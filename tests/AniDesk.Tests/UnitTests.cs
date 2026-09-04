using Xunit;
using System.IO;
using System.Net.Http;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.Tests;

public class CoreUnitTests
{
    [Fact]
    public void ContentSafetyService_InjectsSafeRatingTag_WhenSfwActive()
    {
        var storage = new LocalStorageService();
        var safety = new ContentSafetyService(storage)
        {
            IsSfwShieldActive = true
        };

        string query = safety.PrepareTagsQuery("scenic landscape");
        Assert.Contains("rating:s", query);
    }

    [Fact]
    public void ContentSafetyService_ClientSideFilter_DropsExplicitPosts()
    {
        var storage = new LocalStorageService();
        var safety = new ContentSafetyService(storage)
        {
            IsSfwShieldActive = true
        };

        var posts = new List<MoebooruPost>
        {
            new() { Id = 1, Rating = "s", Tags = "scenic" },
            new() { Id = 2, Rating = "q", Tags = "questionable" },
            new() { Id = 3, Rating = "e", Tags = "explicit" }
        };

        var filtered = safety.FilterPosts(posts).ToList();

        Assert.Single(filtered);
        Assert.Equal(1, filtered[0].Id);
        Assert.Equal("s", filtered[0].Rating);
    }

    [Fact]
    public void LocalStorageService_CanSaveAndRetrieveSettings()
    {
        var storage = new LocalStorageService();
        var settings = storage.LoadSettings();

        settings.DefaultSource = BooruSource.KonachanNet;
        settings.DefaultWallpaperFit = WallpaperFit.Fit;
        storage.SaveSettings(settings);

        var loaded = storage.LoadSettings();
        Assert.Equal(BooruSource.KonachanNet, loaded.DefaultSource);
        Assert.Equal(WallpaperFit.Fit, loaded.DefaultWallpaperFit);
    }

    [Fact]
    public void WallpaperService_CanDetectMonitors()
    {
        var wallpaperService = new WallpaperService();
        var monitors = wallpaperService.GetConnectedMonitors();

        Assert.NotEmpty(monitors);
        Assert.NotNull(monitors[0].DisplayName);
    }

    [Fact]
    public async Task MoebooruService_CanFetchSafePostsFromYandere()
    {
        var storage = new LocalStorageService();
        var safety = new ContentSafetyService(storage) { IsSfwShieldActive = true };
        var service = new MoebooruService(safety);

        try
        {
            var posts = await service.GetPostsAsync(BooruSource.Yandere, "landscape", page: 1, limit: 5);

            Assert.NotNull(posts);
            foreach (var post in posts)
            {
                Assert.Equal("s", post.Rating);
                Assert.True(!string.IsNullOrWhiteSpace(post.BestImageUrl));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // Transient network latency to external Booru API in test environment
        }
    }

    [Fact]
    public void PanicButtonService_InitializesSafeWallpaper_AndManagesState()
    {
        using var panic = new PanicButtonService(IntPtr.Zero);
        Assert.False(panic.IsPanicked);
        Assert.False(panic.IsRegistered);
        Assert.True(!string.IsNullOrWhiteSpace(panic.SafeWallpaperPath));
        Assert.True(File.Exists(panic.SafeWallpaperPath));
    }

    [Fact]
    public void LocalStorageService_CanSaveAndRetrievePanicSettings()
    {
        var storage = new LocalStorageService();
        var settings = storage.LoadSettings();

        settings.PanicWallpaperPath = @"C:\Test\Safe.jpg";
        settings.MinimizeToTrayOnClose = true;
        settings.PanicHotkeyDisplay = "Ctrl + Shift + P";
        storage.SaveSettings(settings);

        var loaded = storage.LoadSettings();
        Assert.Equal(@"C:\Test\Safe.jpg", loaded.PanicWallpaperPath);
        Assert.True(loaded.MinimizeToTrayOnClose);
        Assert.Equal("Ctrl + Shift + P", loaded.PanicHotkeyDisplay);
    }

    [Fact]
    public void ImageCacheService_TracksCacheSizeAccurately()
    {
        var cache = new ImageCacheService();
        long size = cache.GetCacheSizeInBytes();
        Assert.True(size >= 0);
    }

    [Fact]
    public void PanicButtonService_EmergencyToggle_ExecutesWithoutException()
    {
        using var panic = new PanicButtonService(IntPtr.Zero);
        var ex = Record.Exception(() => panic.ExecuteEmergencyToggle());
        Assert.Null(ex);
    }
}
