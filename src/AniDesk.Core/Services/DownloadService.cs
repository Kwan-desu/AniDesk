using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public class DownloadItem : ObservableObject
{
    public MoebooruPost Post { get; set; } = null!;
    public string TargetFilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(TargetFilePath);

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private DownloadStatus _status = DownloadStatus.Queued;
    public DownloadStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public interface IDownloadService
{
    ObservableCollection<DownloadItem> Downloads { get; }
    Task<DownloadItem> DownloadPostAsync(MoebooruPost post, string? customFolder = null, CancellationToken cancellationToken = default);
}

public class DownloadService : IDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _storageService;

    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    public DownloadService(ILocalStorageService storageService, HttpClient? httpClient = null)
    {
        _storageService = storageService;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AniDesk/1.0");
    }

    public async Task<DownloadItem> DownloadPostAsync(MoebooruPost post, string? customFolder = null, CancellationToken cancellationToken = default)
    {
        string targetDir = customFolder ?? _storageService.GetDownloadDirectory();
        Directory.CreateDirectory(targetDir);

        string ext = Path.GetExtension(post.BestImageUrl);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        string authorPart = string.IsNullOrWhiteSpace(post.Author) ? "" : $"_{post.Author}";
        string safeFileName = $"{post.SourceProvider}_{post.Id}{authorPart}_{post.Width}x{post.Height}{ext}";

        // Remove any invalid chars
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            safeFileName = safeFileName.Replace(c, '_');
        }

        string targetPath = Path.Combine(targetDir, safeFileName);

        var downloadItem = new DownloadItem
        {
            Post = post,
            TargetFilePath = targetPath,
            Status = DownloadStatus.Downloading,
            Progress = 0
        };

        Downloads.Insert(0, downloadItem);

        try
        {
            using var response = await _httpClient.GetAsync(post.BestImageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? post.FileSize;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                totalRead += read;

                if (totalBytes > 0)
                {
                    downloadItem.Progress = (double)totalRead / totalBytes * 100.0;
                }
            }

            downloadItem.Progress = 100;
            downloadItem.Status = DownloadStatus.Completed;
        }
        catch (Exception ex)
        {
            downloadItem.Status = DownloadStatus.Failed;
            downloadItem.ErrorMessage = ex.Message;
        }

        return downloadItem;
    }
}
