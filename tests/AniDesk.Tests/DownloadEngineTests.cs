using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using AniDesk.App.Services;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.Tests;

public class DownloadEngineTests
{
    [Fact]
    public void LruMemoryCache_EvictsLeastRecentlyUsed_WhenExceedingCapacity()
    {
        var cache = new LruMemoryCache<string, string>(3);

        cache.Set("A", "ValueA");
        cache.Set("B", "ValueB");
        cache.Set("C", "ValueC");

        Assert.Equal(3, cache.Count);

        // Access "A" so "B" becomes the LRU item
        var a = cache.Get("A");
        Assert.Equal("ValueA", a);

        // Insert "D" -> "B" should be evicted
        cache.Set("D", "ValueD");

        Assert.Equal(3, cache.Count);
        Assert.NotNull(cache.Get("A"));
        Assert.Null(cache.Get("B")); // B evicted
        Assert.NotNull(cache.Get("C"));
        Assert.NotNull(cache.Get("D"));
    }

    [Fact]
    public void LruMemoryCache_Clear_RemovesAllItems()
    {
        var cache = new LruMemoryCache<string, int>(5);
        cache.Set("one", 1);
        cache.Set("two", 2);

        Assert.Equal(2, cache.Count);
        cache.Clear();
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet("one", out _));
    }

    [Fact]
    public void DownloadService_ClearCompleted_RemovesFinishedDownloads()
    {
        var storage = new LocalStorageService();
        var downloadService = new DownloadService(storage, null, action => action());

        var item1 = new DownloadItem { TargetFilePath = "test1.jpg" };
        item1.Status = DownloadStatus.Completed;

        var item2 = new DownloadItem { TargetFilePath = "test2.jpg" };
        item2.Status = DownloadStatus.Downloading;

        var item3 = new DownloadItem { TargetFilePath = "test3.jpg" };
        item3.Status = DownloadStatus.Failed;

        downloadService.Downloads.Add(item1);
        downloadService.Downloads.Add(item2);
        downloadService.Downloads.Add(item3);

        Assert.Equal(3, downloadService.Downloads.Count);

        downloadService.ClearCompleted();

        // item2 (Downloading) remains, item1 and item3 removed
        Assert.Single(downloadService.Downloads);
        Assert.Equal(DownloadStatus.Downloading, downloadService.Downloads[0].Status);
    }

    [Fact]
    public void DownloadService_CancelDownload_CancelsActiveItem()
    {
        var storage = new LocalStorageService();
        var downloadService = new DownloadService(storage, null, action => action());

        var item = new DownloadItem { TargetFilePath = "test_cancel.jpg" };
        item.Status = DownloadStatus.Downloading;
        var cts = new System.Threading.CancellationTokenSource();
        item.Cts = cts;

        downloadService.Downloads.Add(item);

        bool cancelled = downloadService.CancelDownload(item.Id);

        Assert.True(cancelled);
        Assert.True(cts.IsCancellationRequested);
    }
}
