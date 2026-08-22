using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AniDesk.Core.Services;

public interface IImageCacheService
{
    Task<string> GetCachedImagePathAsync(string imageUrl, CancellationToken cancellationToken = default);
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
                                File.Move(tempFile, cachedPath, overwrite: true);
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
        }
        catch { }
    }

    public long GetCacheSizeInBytes()
    {
        try
        {
            if (!Directory.Exists(_cacheFolder)) return 0;
            var di = new DirectoryInfo(_cacheFolder);
            return di.GetFiles().Sum(fi => fi.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static string GetHashedFileName(string url)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
        string hashStr = Convert.ToHexString(hash).ToLowerInvariant();
        string ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        return $"{hashStr}{ext}";
    }
}
