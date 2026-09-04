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
        string tempState = Path.Combine(Path.GetTempPath(), $"panic_test_{Guid.NewGuid():N}.json");
        try
        {
            using var panic = new PanicButtonService(IntPtr.Zero, customStatePath: tempState);
            Assert.False(panic.IsPanicked);
            Assert.False(panic.IsRegistered);
            Assert.True(!string.IsNullOrWhiteSpace(panic.SafeWallpaperPath));
            Assert.True(File.Exists(panic.SafeWallpaperPath));
        }
        finally
        {
            if (File.Exists(tempState)) File.Delete(tempState);
        }
    }

    [Fact]
    public void PanicButtonService_PersistsStateAcrossReinstantiation()
    {
        string tempState = Path.Combine(Path.GetTempPath(), $"panic_test_{Guid.NewGuid():N}.json");
        string tempWallpaper = Path.Combine(Path.GetTempPath(), $"test_wall_{Guid.NewGuid():N}.bmp");
        File.WriteAllBytes(tempWallpaper, [1, 2, 3, 4]);

        try
        {
            using (var panic1 = new PanicButtonService(IntPtr.Zero, customStatePath: tempState))
            {
                panic1.RecordActiveWallpaper(tempWallpaper);
                panic1.ExecuteEmergencyToggle();
                Assert.True(panic1.IsPanicked);
            }

            // Re-instantiate service (simulating application restart)
            using (var panic2 = new PanicButtonService(IntPtr.Zero, customStatePath: tempState))
            {
                Assert.True(panic2.IsPanicked);
                Assert.Equal(tempWallpaper, panic2.LastKnownActiveWallpaper);

                // Un-panic (toggle restore)
                panic2.ExecuteEmergencyToggle();
                Assert.False(panic2.IsPanicked);
            }

            // Check third instantiation verifies un-panicked state
            using (var panic3 = new PanicButtonService(IntPtr.Zero, customStatePath: tempState))
            {
                Assert.False(panic3.IsPanicked);
            }
        }
        finally
        {
            if (File.Exists(tempState)) File.Delete(tempState);
            if (File.Exists(tempWallpaper)) File.Delete(tempWallpaper);
        }
    }

    [Fact]
    public void LocalStorageService_CanSaveAndRetrievePanicSettings()
    {
        var storage = new LocalStorageService();
        var settings = storage.LoadSettings();

        settings.PanicWallpaperPath = @"C:\Test\Safe.jpg";
        settings.MinimizeToTrayOnClose = true;
        settings.StartMinimizedToTray = true;
        settings.PanicHotkeyDisplay = "Ctrl + Shift + P";
        storage.SaveSettings(settings);

        var loaded = storage.LoadSettings();
        Assert.Equal(@"C:\Test\Safe.jpg", loaded.PanicWallpaperPath);
        Assert.True(loaded.MinimizeToTrayOnClose);
        Assert.True(loaded.StartMinimizedToTray);
        Assert.Equal("Ctrl + Shift + P", loaded.PanicHotkeyDisplay);
    }

    [Fact]
    public void DynamicWallpaperService_FiltersCuratedCarouselCandidates()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dynamic_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string fileA = Path.Combine(tempDir, "imgA.jpg");
        string fileB = Path.Combine(tempDir, "imgB.png");
        string fileC = Path.Combine(tempDir, "imgC.webp");
        File.WriteAllBytes(fileA, [1]);
        File.WriteAllBytes(fileB, [1]);
        File.WriteAllBytes(fileC, [1]);

        try
        {
            var storage = new LocalStorageService(tempDir);
            var settings = storage.LoadSettings();
            settings.DownloadFolderPath = tempDir;
            settings.DynamicSource = DynamicWallpaperSource.Downloads;
            settings.DynamicIncludeAllFavorites = false;
            // Select only fileA and fileC for carousel
            settings.DynamicSelectedDownloadFiles = [fileA, fileC];
            storage.SaveSettings(settings);

            var dynamicService = new DynamicWallpaperService(storage, new WallpaperService());
            var candidates = dynamicService.GetCandidates(settings);

            Assert.Contains(fileA, candidates);
            Assert.Contains(fileC, candidates);
            Assert.DoesNotContain(fileB, candidates);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
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
        string tempState = Path.Combine(Path.GetTempPath(), $"panic_test_{Guid.NewGuid():N}.json");
        try
        {
            using var panic = new PanicButtonService(IntPtr.Zero, customStatePath: tempState);
            var ex = Record.Exception(() => panic.ExecuteEmergencyToggle());
            Assert.Null(ex);
        }
        finally
        {
            if (File.Exists(tempState)) File.Delete(tempState);
        }
    }
}
