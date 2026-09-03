using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using AniDesk.Core.Services;

namespace AniDesk.App.Converters;

/// <summary>
/// High-performance zero-LOH async image loader. Streams directly from disk
/// without byte array allocations on the Large Object Heap, bounds both dimensions
/// to protect texture memory, and uses WeakReferences so memory can be reclaimed under pressure.
/// </summary>
public static class AsyncImageLoader
{
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> _weakCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<BitmapSource?>> _inFlightTasks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim _decodeThrottle = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 6));

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

        int dw = Math.Clamp(GetDecodeWidth(img), 80, 960);

        // 1. Weak cache lookup (zero allocation)
        if (_weakCache.TryGetValue(url, out var weakRef) && weakRef.TryGetTarget(out var cachedBmp))
        {
            img.Source = cachedBmp;
            return;
        }

        // 2. Fetch/Decode via coalesced in-flight task
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
            // Ignore cancelled or aborted decodes
        }
    }

    private static async Task<BitmapSource?> LoadBitmapAsync(string url, int decodeWidth)
    {
        try
        {
            var cache = App.Services.GetService<IImageCacheService>();
            string localPath = url;

            if (cache != null && !File.Exists(url))
            {
                localPath = await cache.GetCachedImagePathAsync(url);
            }

            if (!File.Exists(localPath))
            {
                return null;
            }

            await _decodeThrottle.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        // Open stream directly without allocating byte[] on LOH
                        using var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                        if (fileStream.Length == 0) return null;

                        var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                        if (decoder.Frames.Count == 0) return null;

                        var frame = decoder.Frames[0];
                        double aspect = (double)frame.PixelWidth / Math.Max(1, frame.PixelHeight);

                        fileStream.Position = 0;

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = fileStream;
                        bmp.DecodePixelWidth = decodeWidth;

                        // Dual clamping: prevent tall 4-koma/character art from blowing up vertical memory
                        if (aspect < 0.55)
                        {
                            bmp.DecodePixelHeight = (int)(decodeWidth / 0.55);
                        }

                        bmp.EndInit();
                        bmp.Freeze(); // Required for cross-thread access and memory optimization

                        _weakCache[url] = new WeakReference<BitmapSource>(bmp);
                        return (BitmapSource)bmp;
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
            }
            finally
            {
                _decodeThrottle.Release();
            }
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

    public static void PurgeMemoryCache()
    {
        _weakCache.Clear();
    }
}
