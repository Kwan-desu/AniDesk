using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AniDesk.Core.Services;

public interface IImageCacheService
{
    Task<string> GetCachedImagePathAsync(string imageUrl, CancellationToken cancellationToken = default);
    Task<FileStream?> OpenReadStreamAsync(string imageUrl, CancellationToken cancellationToken = default);
    [Obsolete("Use OpenReadStreamAsync to avoid Large Object Heap allocations.")]
    Task<byte[]> GetImageBytesAsync(string imageUrl, CancellationToken cancellationToken = default);
    void PreloadThumbnails(IEnumerable<string> urls);
    void ClearCache();
    long GetCacheSizeInBytes();
}

public class ImageCacheService : IImageCacheService
{
    private readonly string _cacheFolder;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, Task<string>> _inFlightDownloads = new();
    private long _totalCacheSizeBytes = -1;

    public ImageCacheService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            EnableMultipleHttp2Connections = true
        });
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(25);

        _cacheFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniDesk",
            "Cache",
            "Thumbnails"
        );

        Directory.CreateDirectory(_cacheFolder);

        // Compute initial cache size in background to never block UI startup
        Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(_cacheFolder))
                {
                    var di = new DirectoryInfo(_cacheFolder);
                    long total = di.GetFiles().Sum(fi => fi.Length);
                    Interlocked.Exchange(ref _totalCacheSizeBytes, total);
                }
                else
                {
                    Interlocked.Exchange(ref _totalCacheSizeBytes, 0);
                }
            }
            catch
            {
                Interlocked.Exchange(ref _totalCacheSizeBytes, 0);
            }
        });
    }

    public void PreloadThumbnails(IEnumerable<string> urls)
    {
        Task.Run(async () =>
        {
            foreach (var url in urls.Take(40))
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    _ = GetCachedImagePathAsync(url);
                }
            }
        });
    }

    public Task<string> GetCachedImagePathAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Task.FromResult(string.Empty);
        }

        string fileName = GetHashedFileName(imageUrl);
        string cachedPath = Path.Combine(_cacheFolder, fileName);

        if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
        {
            return Task.FromResult(cachedPath);
        }

        return _inFlightDownloads.GetOrAdd(imageUrl, async (url) =>
        {
            try
            {
                if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                {
                    return cachedPath;
                }

                string tempFile = Path.Combine(_cacheFolder, $"dl_{Guid.NewGuid():N}.tmp");

                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get, url);
                        if (url.Contains("yande.re", StringComparison.OrdinalIgnoreCase))
                        {
                            request.Headers.Referrer = new Uri("https://yande.re/");
                        }
                        else if (url.Contains("konachan", StringComparison.OrdinalIgnoreCase))
                        {
                            request.Headers.Referrer = new Uri("https://konachan.net/");
                        }

                        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                            {
                                await response.Content.CopyToAsync(fileStream, cancellationToken);
                            }

                            if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 0)
                            {
                                var fi = new FileInfo(tempFile);
                                long len = fi.Length;
                                File.Move(tempFile, cachedPath, overwrite: true);
                                Interlocked.Add(ref _totalCacheSizeBytes, len);
                                return cachedPath;
                            }
                        }
                    }
                    catch when (attempt < 2)
                    {
                        await Task.Delay(200, cancellationToken);
                    }
                    finally
                    {
                        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    }
                }

                if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 0)
                {
                    return cachedPath;
                }

                return url;
            }
            catch
            {
                return url;
            }
            finally
            {
                _inFlightDownloads.TryRemove(imageUrl, out _);
            }
        });
    }

    public async Task<FileStream?> OpenReadStreamAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        string localPath = await GetCachedImagePathAsync(imageUrl, cancellationToken);
        if (File.Exists(localPath))
        {
            return new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous);
        }
        return null;
    }

    public async Task<byte[]> GetImageBytesAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        string localPath = await GetCachedImagePathAsync(imageUrl, cancellationToken);
        if (File.Exists(localPath))
        {
            return await File.ReadAllBytesAsync(localPath, cancellationToken);
        }

        return await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
    }

    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheFolder))
            {
                var files = Directory.GetFiles(_cacheFolder);
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
            }
            Interlocked.Exchange(ref _totalCacheSizeBytes, 0);
        }
        catch { }
    }

    public long GetCacheSizeInBytes()
    {
        long current = Interlocked.Read(ref _totalCacheSizeBytes);
        if (current >= 0) return current;

        try
        {
            if (!Directory.Exists(_cacheFolder)) return 0;
            var di = new DirectoryInfo(_cacheFolder);
            long size = di.GetFiles().Sum(fi => fi.Length);
            Interlocked.Exchange(ref _totalCacheSizeBytes, size);
            return size;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetHashedFileName(string url)
    {
        Span<byte> utf8 = stackalloc byte[Encoding.UTF8.GetByteCount(url)];
        Encoding.UTF8.GetBytes(url, utf8);
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(utf8, hash);
        string hashStr = Convert.ToHexString(hash).ToLowerInvariant();
        string ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5) ext = ".jpg";
        return $"{hashStr}{ext}";
    }
}
