using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public class DownloadProgressEventArgs : EventArgs
{
    public DownloadItem Item { get; }
    public double Progress { get; }
    public long BytesReceived { get; }
    public long TotalBytes { get; }
    public double SpeedBytesPerSecond { get; }

    public DownloadProgressEventArgs(DownloadItem item, double progress, long bytesReceived, long totalBytes, double speed)
    {
        Item = item;
        Progress = progress;
        BytesReceived = bytesReceived;
        TotalBytes = totalBytes;
        SpeedBytesPerSecond = speed;
    }
}

public class DownloadCompletedEventArgs : EventArgs
{
    public DownloadItem Item { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public DownloadCompletedEventArgs(DownloadItem item, bool success, string? errorMessage = null)
    {
        Item = item;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

public partial class DownloadItem : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public MoebooruPost Post { get; set; } = null!;
    public string TargetFilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(TargetFilePath);

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private DownloadStatus _status = DownloadStatus.Queued;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private long _bytesReceived;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private double _downloadSpeedMbps;

    public DateTime Timestamp { get; set; } = DateTime.Now;

    [JsonIgnore]
    internal CancellationTokenSource? Cts { get; set; }

    public void Cancel()
    {
        Cts?.Cancel();
    }
}

public interface IDownloadService
{
    ObservableCollection<DownloadItem> Downloads { get; }
    int ActiveDownloadsCount { get; }

    event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
    event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

    Task<DownloadItem> DownloadPostAsync(MoebooruPost post, string? customFolder = null, CancellationToken cancellationToken = default);
    bool CancelDownload(string downloadId);
    void CancelAll();
    void ClearCompleted();
}

public class DownloadService : IDownloadService
{
    private const int BufferSize = 81920; // 80 KB
    private const int ProgressThrottleMilliseconds = 100; // ~10 updates/sec max

    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _storageService;
    private readonly ConcurrentDictionary<string, DownloadItem> _activeItems = new();
    private readonly Action<Action>? _uiDispatcher;

    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    public int ActiveDownloadsCount => _activeItems.Count;

    public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
    public event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;

    public DownloadService(
        ILocalStorageService storageService,
        HttpClient? httpClient = null,
        Action<Action>? uiDispatcher = null)
    {
        _storageService = storageService;
        _uiDispatcher = uiDispatcher;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            EnableMultipleHttp2Connections = true
        });
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AniDesk/1.0");
    }

    public async Task<DownloadItem> DownloadPostAsync(
        MoebooruPost post,
        string? customFolder = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);

        string targetDir = customFolder ?? _storageService.GetDownloadDirectory();
        Directory.CreateDirectory(targetDir);

        // 1. Robust URL parsing: extract file extension using Uri.AbsolutePath
        string rawUrl = post.BestImageUrl;
        string ext = ExtractExtensionFromUrl(rawUrl);

        string authorPart = string.IsNullOrWhiteSpace(post.Author) ? "" : $"_{post.Author}";
        string safeFileName = $"{post.SourceProvider}_{post.Id}{authorPart}_{post.Width}x{post.Height}{ext}";

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            safeFileName = safeFileName.Replace(c, '_');
        }

        string targetPath = Path.Combine(targetDir, safeFileName);

        var downloadItem = new DownloadItem
        {
            Post = post,
            TargetFilePath = targetPath,
            Status = DownloadStatus.Queued,
            Progress = 0,
            TotalBytes = post.FileSize
        };

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        downloadItem.Cts = linkedCts;
        _activeItems[downloadItem.Id] = downloadItem;

        // Thread-safe dispatch to ObservableCollection
        DispatchToUI(() => Downloads.Insert(0, downloadItem));

        // Offload entire streaming execution to background thread pool
        _ = Task.Run(() => ExecuteDownloadAsync(downloadItem, rawUrl, targetPath, linkedCts.Token), CancellationToken.None);

        return downloadItem;
    }

    private async Task ExecuteDownloadAsync(
        DownloadItem item,
        string sourceUrl,
        string targetPath,
        CancellationToken ct)
    {
        // Two-phase atomic temp path: {targetPath}.{guid}.download
        string tempPath = $"{targetPath}.{Guid.NewGuid():N}.download";

        var stopwatch = Stopwatch.StartNew();
        long lastReportedBytes = 0;
        long lastReportedTimeMs = 0;

        try
        {
            item.Status = DownloadStatus.Downloading;

            using var response = await _httpClient.GetAsync(
                sourceUrl,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            ).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? item.Post.FileSize;
            item.TotalBytes = totalBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            // Phase 1: Stream to temp file with FileShare.ReadWrite to avoid locking file scanners
            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite,
                BufferSize,
                useAsync: true))
            {
                byte[] buffer = new byte[BufferSize];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    totalRead += read;

                    long currentMs = stopwatch.ElapsedMilliseconds;
                    if (currentMs - lastReportedTimeMs >= ProgressThrottleMilliseconds || (totalBytes > 0 && totalRead == totalBytes))
                    {
                        double elapsedSeconds = (currentMs - lastReportedTimeMs) / 1000.0;
                        long bytesSinceLast = totalRead - lastReportedBytes;
                        double speedMbps = elapsedSeconds > 0 ? (bytesSinceLast * 8.0) / (elapsedSeconds * 1_000_000.0) : 0;

                        double progressPct = totalBytes > 0 ? Math.Min(100.0, (double)totalRead / totalBytes * 100.0) : 0;

                        lastReportedBytes = totalRead;
                        lastReportedTimeMs = currentMs;

                        // Update item properties on UI thread if needed
                        DispatchToUI(() =>
                        {
                            item.BytesReceived = totalRead;
                            item.Progress = progressPct;
                            item.DownloadSpeedMbps = Math.Round(speedMbps, 2);
                        });

                        DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs(
                            item, progressPct, totalRead, totalBytes, speedMbps));
                    }
                }

                // Force hardware flush before atomic file promotion
                await fileStream.FlushAsync(ct).ConfigureAwait(false);
            }

            // Verification: Ensure downloaded file exists and is not 0 bytes
            var fileInfo = new FileInfo(tempPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                throw new IOException("Downloaded file is empty or corrupted.");
            }

            // Phase 2: Atomic rename/move to target destination
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);

            DispatchToUI(() =>
            {
                item.Progress = 100;
                item.Status = DownloadStatus.Completed;
            });

            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(item, true));
        }
        catch (OperationCanceledException)
        {
            DispatchToUI(() =>
            {
                item.Status = DownloadStatus.Cancelled;
                item.ErrorMessage = "Download cancelled by user.";
            });
            CleanupTempFile(tempPath);
            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(item, false, "Cancelled"));
        }
        catch (Exception ex)
        {
            DispatchToUI(() =>
            {
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage = ex.Message;
            });
            CleanupTempFile(tempPath);
            DownloadCompleted?.Invoke(this, new DownloadCompletedEventArgs(item, false, ex.Message));
        }
        finally
        {
            stopwatch.Stop();
            _activeItems.TryRemove(item.Id, out _);
            item.Cts?.Dispose();
            item.Cts = null;
        }
    }

    public bool CancelDownload(string downloadId)
    {
        if (_activeItems.TryGetValue(downloadId, out var item))
        {
            item.Cancel();
            return true;
        }

        var match = Downloads.FirstOrDefault(d => d.Id == downloadId);
        if (match != null)
        {
            match.Cancel();
            return true;
        }

        return false;
    }

    public void CancelAll()
    {
        foreach (var item in _activeItems.Values)
        {
            item.Cancel();
        }
    }

    public void ClearCompleted()
    {
        DispatchToUI(() =>
        {
            var completed = Downloads
                .Where(d => d.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
                .ToList();

            foreach (var item in completed)
            {
                Downloads.Remove(item);
            }
        });
    }

    private static string ExtractExtensionFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            string ext = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                return ext.ToLowerInvariant();
            }
        }
        return ".jpg";
    }

    private static void CleanupTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch { /* Suppress background cleanup failures */ }
    }

    private void DispatchToUI(Action action)
    {
        if (_uiDispatcher != null)
        {
            _uiDispatcher(action);
        }
        else if (SynchronizationContext.Current != null)
        {
            SynchronizationContext.Current.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
}
