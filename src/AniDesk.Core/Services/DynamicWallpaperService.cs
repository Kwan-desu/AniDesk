using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public interface IDynamicWallpaperService : IDisposable
{
    bool IsRunning { get; }
    string? CurrentWallpaper { get; }
    DateTime? NextRunTimeUtc { get; }
    void Start();
    void Stop();
    void UpdateSettings(AppSettings settings);
    Task<bool> TriggerNextAsync();
    List<string> GetCandidates(AppSettings? settings = null);
    List<string> GetCandidates(DynamicWallpaperSource source);
    event EventHandler<string>? WallpaperChanged;
}

public sealed class DynamicWallpaperService : IDynamicWallpaperService
{
    private readonly ILocalStorageService _storageService;
    private readonly IWallpaperService _wallpaperService;
    private Timer? _timer;
    private readonly Random _rng = new();
    private string? _lastWallpaper;
    private int _isProcessing;

    public bool IsRunning => _timer != null;
    public string? CurrentWallpaper => _lastWallpaper;
    public DateTime? NextRunTimeUtc { get; private set; }
    public event EventHandler<string>? WallpaperChanged;

    public DynamicWallpaperService(ILocalStorageService storageService, IWallpaperService wallpaperService)
    {
        _storageService = storageService;
        _wallpaperService = wallpaperService;
    }

    public void Start()
    {
        var settings = _storageService.LoadSettings();
        if (!settings.EnableDynamicWallpaper)
        {
            Stop();
            return;
        }

        int minutes = Math.Clamp(settings.DynamicWallpaperIntervalMinutes, 1, 1440);
        var interval = TimeSpan.FromMinutes(minutes);
        NextRunTimeUtc = DateTime.UtcNow.Add(interval);

        _timer?.Dispose();
        _timer = new Timer(async _ => await OnTimerTickAsync(), null, interval, interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        NextRunTimeUtc = null;
    }

    public void UpdateSettings(AppSettings settings)
    {
        if (settings.EnableDynamicWallpaper)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    private async Task OnTimerTickAsync()
    {
        var settings = _storageService.LoadSettings();
        int minutes = Math.Clamp(settings.DynamicWallpaperIntervalMinutes, 1, 1440);
        NextRunTimeUtc = DateTime.UtcNow.Add(TimeSpan.FromMinutes(minutes));
        await TriggerNextAsync();
    }

    public async Task<bool> TriggerNextAsync()
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            var settings = _storageService.LoadSettings();
            var candidates = GetCandidates(settings);

            if (candidates.Count == 0)
            {
                return false;
            }

            string chosen;
            if (candidates.Count == 1)
            {
                chosen = candidates[0];
            }
            else if (settings.DynamicShuffle)
            {
                var pool = candidates.Where(c => !string.Equals(c, _lastWallpaper, StringComparison.OrdinalIgnoreCase)).ToList();
                chosen = pool.Count > 0 ? pool[_rng.Next(pool.Count)] : candidates[_rng.Next(candidates.Count)];
            }
            else
            {
                int idx = candidates.FindIndex(c => string.Equals(c, _lastWallpaper, StringComparison.OrdinalIgnoreCase));
                chosen = candidates[(idx + 1) % candidates.Count];
            }

            _lastWallpaper = chosen;
            bool success = await _wallpaperService.SetWallpaperAsync(chosen, monitorIndex: -1, settings.DefaultWallpaperFit);
            if (success)
            {
                WallpaperChanged?.Invoke(this, chosen);
            }
            return success;
        }
        catch
        {
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
        }
    }

    public List<string> GetCandidates(DynamicWallpaperSource source)
    {
        var settings = _storageService.LoadSettings();
        settings.DynamicSource = source;
        return GetCandidates(settings);
    }

    public List<string> GetCandidates(AppSettings? settings = null)
    {
        settings ??= _storageService.LoadSettings();
        var result = new List<string>();

        // 1. Curated or All Downloaded Images for Carousel
        string downloadDir = _storageService.GetDownloadDirectory();
        if (Directory.Exists(downloadDir))
        {
            string[] validExtensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp"];
            try
            {
                var allDownloaded = Directory.GetFiles(downloadDir)
                    .Where(f => validExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                if (settings.DynamicSelectedDownloadFiles != null && settings.DynamicSelectedDownloadFiles.Count > 0)
                {
                    var selectedSet = new HashSet<string>(settings.DynamicSelectedDownloadFiles, StringComparer.OrdinalIgnoreCase);
                    var curated = allDownloaded.Where(f => selectedSet.Contains(f) || selectedSet.Contains(Path.GetFileName(f))).ToList();
                    result.AddRange(curated);
                }
                else if (settings.DynamicSource is DynamicWallpaperSource.Downloads or DynamicWallpaperSource.Both)
                {
                    result.AddRange(allDownloaded);
                }
            }
            catch { }
        }

        // 2. All in Favorite Section
        if (settings.DynamicIncludeAllFavorites || settings.DynamicSource is DynamicWallpaperSource.Favorites or DynamicWallpaperSource.Both)
        {
            try
            {
                var favorites = _storageService.LoadFavorites();
                foreach (var fav in favorites)
                {
                    string target = !string.IsNullOrEmpty(fav.FileUrl) ? fav.FileUrl : fav.SampleUrl;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        result.Add(target);
                    }
                }
            }
            catch { }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
