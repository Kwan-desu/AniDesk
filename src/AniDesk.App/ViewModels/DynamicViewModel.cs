using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class CarouselDownloadItem : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileSizeText { get; set; } = string.Empty;
    public string ExtensionBadge { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelectedInCarousel;

    [ObservableProperty]
    private bool _isCurrentlyActive;
}

public partial class DynamicViewModel : ObservableObject
{
    private readonly IDynamicWallpaperService _dynamicService;
    private readonly ILocalStorageService _storageService;
    private readonly IWallpaperService _wallpaperService;
    private readonly DispatcherTimer _countdownTimer;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _intervalMinutes = 5;

    [ObservableProperty]
    private bool _isShuffle = true;

    [ObservableProperty]
    private bool _includeAllFavorites = true;

    [ObservableProperty]
    private string _currentWallpaper = string.Empty;

    [ObservableProperty]
    private string _countdownText = "Ready to shuffle";

    [ObservableProperty]
    private string _poolSummary = "Calculating rotation pool...";

    [ObservableProperty]
    private int _selectedDownloadsCount;

    [ObservableProperty]
    private int _totalDownloadsCount;

    [ObservableProperty]
    private int _favoritesCount;

    [ObservableProperty]
    private bool _hasDownloadedFiles;

    [ObservableProperty]
    private bool _isShuffling;

    public ObservableCollection<CarouselDownloadItem> DownloadedWallpapers { get; } = new();
    public ObservableCollection<string> CarouselPreviewList { get; } = new();

    public int[] QuickIntervals { get; } = [1, 5, 10, 15, 30, 60, 120];

    public DynamicViewModel(
        IDynamicWallpaperService dynamicService,
        ILocalStorageService storageService,
        IWallpaperService wallpaperService)
    {
        _dynamicService = dynamicService;
        _storageService = storageService;
        _wallpaperService = wallpaperService;

        _dynamicService.WallpaperChanged += OnWallpaperChanged;

        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += OnCountdownTick;

        LoadSettings();
        _countdownTimer.Start();
    }

    public void LoadSettings()
    {
        var settings = _storageService.LoadSettings();
        IsEnabled = settings.EnableDynamicWallpaper;
        IntervalMinutes = Math.Clamp(settings.DynamicWallpaperIntervalMinutes, 1, 1440);
        IsShuffle = settings.DynamicShuffle;
        IncludeAllFavorites = settings.DynamicIncludeAllFavorites;

        CurrentWallpaper = _dynamicService.CurrentWallpaper ?? string.Empty;

        _ = RefreshDownloadsAsync();
        UpdatePoolSummary();
    }

    public void RefreshDownloads() => _ = RefreshDownloadsAsync();

    public async Task RefreshDownloadsAsync()
    {
        // Snapshot settings and paths off-thread to avoid lock contention with LocalStorageService
        var (selectedSet, downloadFolderPath, appDataDownloads) = await Task.Run(() =>
        {
            var s = _storageService.LoadSettings();
            var sel = new HashSet<string>(s.DynamicSelectedDownloadFiles ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            string dlPath = string.Empty;
            try { dlPath = _storageService.GetDownloadDirectory(); } catch { }
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AniDesk", "Downloads");
            return (sel, s.DownloadFolderPath ?? string.Empty, appData);
        });

        string[] validExtensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp"];

        // Scan directories on background thread — no UI-thread lock involvement
        var allFiles = await Task.Run(() =>
        {
            var foldersToScan = new List<string>();
            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(string? path)
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && seenFolders.Add(path))
                    foldersToScan.Add(path);
            }

            TryAdd(downloadFolderPath);
            TryAdd(appDataDownloads);

            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<FileInfo>();

            foreach (var folder in foldersToScan)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        if (validExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && seenFiles.Add(file))
                            result.Add(new FileInfo(file));
                    }
                }
                catch
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(folder))
                        {
                            if (validExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && seenFiles.Add(file))
                                result.Add(new FileInfo(file));
                        }
                    }
                    catch { }
                }
            }

            result.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return result;
        });

        // Back on UI thread — update ObservableCollections
        DownloadedWallpapers.Clear();

        TotalDownloadsCount = allFiles.Count;
        HasDownloadedFiles = allFiles.Count > 0;

        string currentWp = CurrentWallpaper;
        foreach (var fi in allFiles)
        {
            string ext = fi.Extension.TrimStart('.').ToUpperInvariant();
            string size = FormatFileSize(fi.Length);
            bool selected = selectedSet.Contains(fi.FullName) || selectedSet.Contains(fi.Name);

            DownloadedWallpapers.Add(new CarouselDownloadItem
            {
                FilePath = fi.FullName,
                FileName = fi.Name,
                FileSizeText = size,
                ExtensionBadge = ext,
                IsSelectedInCarousel = selected,
                IsCurrentlyActive = string.Equals(fi.FullName, currentWp, StringComparison.OrdinalIgnoreCase)
            });
        }

        UpdateSelectionCounts();
        RebuildCarouselPreviewList();
    }

    private void UpdateSelectionCounts()
    {
        SelectedDownloadsCount = DownloadedWallpapers.Count(d => d.IsSelectedInCarousel);

        try
        {
            var favs = _storageService.LoadFavorites();
            FavoritesCount = favs.Count;
        }
        catch
        {
            FavoritesCount = 0;
        }

        UpdatePoolSummary();
    }

    private void UpdatePoolSummary()
    {
        int poolCount = SelectedDownloadsCount + (IncludeAllFavorites ? FavoritesCount : 0);
        if (!IsEnabled)
        {
            PoolSummary = $"Auto Shuffle Paused • {poolCount} wallpapers in configured carousel pool";
        }
        else
        {
            PoolSummary = $"{poolCount} Wallpapers in Carousel Rotation ({SelectedDownloadsCount} Selected Downloads + {(IncludeAllFavorites ? $"{FavoritesCount} Favorites" : "0 Favorites")})";
        }
    }

    private void RebuildCarouselPreviewList()
    {
        CarouselPreviewList.Clear();

        // 1. Selected downloads
        foreach (var item in DownloadedWallpapers.Where(d => d.IsSelectedInCarousel))
        {
            CarouselPreviewList.Add(item.FilePath);
        }

        // 2. Favorites
        if (IncludeAllFavorites)
        {
            try
            {
                var favs = _storageService.LoadFavorites();
                foreach (var f in favs)
                {
                    string target = !string.IsNullOrEmpty(f.FileUrl) ? f.FileUrl : f.SampleUrl;
                    if (!string.IsNullOrWhiteSpace(target) && !CarouselPreviewList.Contains(target, StringComparer.OrdinalIgnoreCase))
                    {
                        CarouselPreviewList.Add(target);
                    }
                }
            }
            catch { }
        }
    }

    private void SaveDynamicSettings()
    {
        var settings = _storageService.LoadSettings();
        settings.EnableDynamicWallpaper = IsEnabled;
        settings.DynamicWallpaperIntervalMinutes = Math.Clamp(IntervalMinutes, 1, 1440);
        settings.DynamicShuffle = IsShuffle;
        settings.DynamicIncludeAllFavorites = IncludeAllFavorites;
        settings.DynamicSelectedDownloadFiles = DownloadedWallpapers
            .Where(d => d.IsSelectedInCarousel)
            .Select(d => d.FilePath)
            .ToList();

        _storageService.SaveSettings(settings);
        _dynamicService.UpdateSettings(settings);

        UpdateSelectionCounts();
        RebuildCarouselPreviewList();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (!IsEnabled || !_dynamicService.IsRunning)
        {
            CountdownText = "Auto Shuffle is paused";
            return;
        }

        if (_dynamicService.NextRunTimeUtc.HasValue)
        {
            var diff = _dynamicService.NextRunTimeUtc.Value - DateTime.UtcNow;
            if (diff.TotalSeconds > 0)
            {
                CountdownText = $"Next shuffle in {diff.Minutes:D2}:{diff.Seconds:D2}";
            }
            else
            {
                CountdownText = "Shuffling desktop wallpaper...";
            }
        }
        else
        {
            CountdownText = $"Rotating every {IntervalMinutes} minutes";
        }
    }

    private void OnWallpaperChanged(object? sender, string path)
    {
        CurrentWallpaper = path;
        foreach (var item in DownloadedWallpapers)
        {
            item.IsCurrentlyActive = string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void ToggleDynamic()
    {
        IsEnabled = !IsEnabled;
        SaveDynamicSettings();
    }

    partial void OnIsEnabledChanged(bool value)
    {
        SaveDynamicSettings();
    }

    partial void OnIntervalMinutesChanged(int value)
    {
        SaveDynamicSettings();
    }

    partial void OnIsShuffleChanged(bool value)
    {
        SaveDynamicSettings();
    }

    partial void OnIncludeAllFavoritesChanged(bool value)
    {
        SaveDynamicSettings();
    }

    [RelayCommand]
    private void SetInterval(int minutes)
    {
        IntervalMinutes = minutes;
    }

    [RelayCommand]
    private async Task ShuffleNowAsync()
    {
        if (IsShuffling) return;
        try
        {
            IsShuffling = true;
            CountdownText = "Applying next wallpaper...";
            bool success = await _dynamicService.TriggerNextAsync();
            if (success)
            {
                CurrentWallpaper = _dynamicService.CurrentWallpaper ?? CurrentWallpaper;
            }
        }
        finally
        {
            IsShuffling = false;
            OnCountdownTick(null, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void ToggleItemSelection(CarouselDownloadItem item)
    {
        if (item == null) return;
        item.IsSelectedInCarousel = !item.IsSelectedInCarousel;
        SaveDynamicSettings();
    }

    [RelayCommand]
    private void SelectAllDownloads()
    {
        foreach (var item in DownloadedWallpapers)
        {
            item.IsSelectedInCarousel = true;
        }
        SaveDynamicSettings();
    }

    [RelayCommand]
    private void DeselectAllDownloads()
    {
        foreach (var item in DownloadedWallpapers)
        {
            item.IsSelectedInCarousel = false;
        }
        SaveDynamicSettings();
    }

    [RelayCommand]
    private async Task RefreshPool()
    {
        await RefreshDownloadsAsync();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        return $"{mb:F1} MB";
    }
}