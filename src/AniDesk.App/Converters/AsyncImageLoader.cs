using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using AniDesk.App.Services;
using AniDesk.Core.Services;

namespace AniDesk.App.Converters;

/// <summary>
/// High-performance 2-tier async image loader.
/// Tier 1: Bounded LRU strong-reference memory cache (150 items) for instant tab transitions.
/// Tier 2: WeakReference fallback for un-cached or recycled items.
/// Streams directly from disk with FileShare.ReadWrite and bounds dimensions to protect texture memory.
/// </summary>
public static class AsyncImageLoader
{
    // Tier 1: 150 items bounded strong-reference LRU (~15-20MB RAM)
    private static readonly LruMemoryCache<string, BitmapSource> _tier1LruCache = new(150, StringComparer.OrdinalIgnoreCase);

    // Tier 2: WeakReference fallback
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> _tier2WeakCache = new(StringComparer.OrdinalIgnoreCase);

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

        string? oldUrl = e.OldValue as string;
        string? newUrl = e.NewValue as string;

        // Prevent redundant reload and visual blinking if URL hasn't changed
        if (string.Equals(oldUrl, newUrl, StringComparison.OrdinalIgnoreCase) && img.Source != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newUrl))
        {
            img.Source = null;
            return;
        }

        // 1. Check Tier 1 (LRU Strong Cache) — synchronous hit, 0 visual flicker
        if (_tier1LruCache.TryGet(newUrl, out var strongBmp) && strongBmp != null)
        {
            img.Source = strongBmp;
            return;
        }

        // 2. Check Tier 2 (Weak Cache) — promote to Tier 1 on hit
        if (_tier2WeakCache.TryGetValue(newUrl, out var weakRef) && weakRef.TryGetTarget(out var weakBmp))
        {
            _tier1LruCache.Set(newUrl, weakBmp);
            img.Source = weakBmp;
            return;
        }

        // 3. Cache Miss: Only clear previous source if old image does not match
        img.Source = null;

        int dw = Math.Clamp(GetDecodeWidth(img), 80, 960);

        // 4. Fetch/Decode via coalesced in-flight task
        try
        {
            var task = _inFlightTasks.GetOrAdd(newUrl, u => LoadBitmapAsync(u, dw));
            var bitmap = await task;

            if (bitmap != null && string.Equals(GetImageUrl(img), newUrl, StringComparison.OrdinalIgnoreCase))
            {
                img.Source = bitmap;
            }
        }
        catch
        {
            // Ignore transient decode failure
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
                    // Resilient read: allow concurrent readers and non-exclusive writers
                    // Retry up to 3 times with backoff for files undergoing anti-virus scans or stream flush
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            using var fileStream = new FileStream(
                                localPath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite | FileShare.Delete,
                                4096,
                                FileOptions.SequentialScan);

                            if (fileStream.Length == 0) return null;

                            var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                            if (decoder.Frames.Count == 0) return null;

                            var frame = decoder.Frames[0];
                            double aspect = (double)frame.PixelWidth / Math.Max(1, frame.PixelHeight);

                            fileStream.Position = 0;

                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                            bmp.StreamSource = fileStream;
                            bmp.DecodePixelWidth = decodeWidth;

                            // Dual clamping: prevent tall 4-koma/character art from blowing up vertical memory
                            if (aspect < 0.55)
                            {
                                bmp.DecodePixelHeight = (int)(decodeWidth / 0.55);
                            }

                            bmp.EndInit();
                            bmp.Freeze(); // Required for cross-thread access and memory optimization

                            // Populate both Tier 1 and Tier 2
                            _tier1LruCache.Set(url, bmp);
                            _tier2WeakCache[url] = new WeakReference<BitmapSource>(bmp);

                            return (BitmapSource)bmp;
                        }
                        catch (IOException) when (attempt < 3)
                        {
                            Thread.Sleep(50 * attempt);
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
                    }
                    return null;
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
        _tier1LruCache.Clear();
        _tier2WeakCache.Clear();
    }
}
