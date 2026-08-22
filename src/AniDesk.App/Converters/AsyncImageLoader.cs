using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using AniDesk.Core.Services;

namespace AniDesk.App.Converters;

/// <summary>
/// Robust async image loader. Guarantees every bound Image control receives
/// its decoded BitmapSource even with heavy UI virtualization and concurrent requests.
/// </summary>
public static class AsyncImageLoader
{
    private const int MaxCacheSize = 100;
    private static readonly LinkedList<string> _lruOrder = new();
    private static readonly Dictionary<string, (LinkedListNode<string> node, BitmapImage bitmap)> _cache = new();
    private static readonly object _cacheLock = new();

    private static readonly ConcurrentDictionary<string, Task<BitmapImage?>> _inFlightTasks = new();

    public static readonly DependencyProperty ImageUrlProperty =
        DependencyProperty.RegisterAttached(
            "ImageUrl",
            typeof(string),
            typeof(AsyncImageLoader),
            new PropertyMetadata(null, OnImageUrlChanged));

    public static readonly DependencyProperty DecodeWidthProperty =
        DependencyProperty.RegisterAttached(
            "DecodeWidth",
            typeof(int),
            typeof(AsyncImageLoader),
            new PropertyMetadata(360));

    public static string? GetImageUrl(DependencyObject o) => (string?)o.GetValue(ImageUrlProperty);
    public static void SetImageUrl(DependencyObject o, string? v) => o.SetValue(ImageUrlProperty, v);
    public static int GetDecodeWidth(DependencyObject o) => (int)o.GetValue(DecodeWidthProperty);
    public static void SetDecodeWidth(DependencyObject o, int v) => o.SetValue(DecodeWidthProperty, v);

    private static async void OnImageUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image img) return;
        img.Source = null;

        string? url = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(url)) return;

        int dw = Math.Clamp(GetDecodeWidth(img), 80, 720);

        // 1. Check in-memory LRU cache
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(url, out var hit))
            {
                _lruOrder.Remove(hit.node);
                _lruOrder.AddFirst(hit.node);
                img.Source = hit.bitmap;
                return;
            }
        }

        // 2. Fetch/Decode via shared in-flight task so all bound controls receive the bitmap
        try
        {
            var task = _inFlightTasks.GetOrAdd(url, u => LoadBitmapAsync(u, dw));
            var bitmap = await task;

            if (bitmap != null && GetImageUrl(img) == url)
            {
                img.Source = bitmap;
            }
        }
        catch
        {
            // Ignore cancelled/aborted operations
        }
    }

    private static async Task<BitmapImage?> LoadBitmapAsync(string url, int decodeWidth)
    {
        try
        {
            var cache = App.Services.GetService<IImageCacheService>();
            string localPath = url;

            if (cache != null && !File.Exists(url))
            {
                localPath = await cache.GetCachedImagePathAsync(url);
            }

            byte[]? imageBytes = null;
            if (File.Exists(localPath))
            {
                imageBytes = await File.ReadAllBytesAsync(localPath);
            }
            else if (cache != null)
            {
                imageBytes = await cache.GetImageBytesAsync(url);
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            var bmp = await Task.Run(() =>
            {
                try
                {
                    using var ms = new MemoryStream(imageBytes);
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.StreamSource = ms;
                    if (decodeWidth > 0)
                    {
                        b.DecodePixelWidth = decodeWidth;
                    }
                    b.EndInit();
                    b.Freeze();
                    return b;
                }
                catch
                {
                    try 
                    { 
                        if (localPath != url && File.Exists(localPath)) 
                        {
                            File.Delete(localPath); 
                        }
                    } 
                    catch { }
                    return null;
                }
            });

            if (bmp != null)
            {
                lock (_cacheLock)
                {
                    if (!_cache.ContainsKey(url))
                    {
                        var node = _lruOrder.AddFirst(url);
                        _cache[url] = (node, bmp);

                        while (_cache.Count > MaxCacheSize)
                        {
                            var oldest = _lruOrder.Last!;
                            _lruOrder.RemoveLast();
                            _cache.Remove(oldest.Value);
                        }
                    }
                }
            }

            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            _inFlightTasks.TryRemove(url, out _);
        }
    }
}
