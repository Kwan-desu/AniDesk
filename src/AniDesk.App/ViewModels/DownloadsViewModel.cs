using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Services;
using AniDesk.Core.Models;

namespace AniDesk.App.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly ILocalStorageService _storageService;
    private readonly IWallpaperService _wallpaperService;
    private volatile bool _isRefreshing;

    public ObservableCollection<DownloadItem> Downloads => _downloadService.Downloads;
    public ObservableCollection<LocalWallpaperItem> LocalWallpapers { get; } = new();

    public bool HasActiveDownloads => Downloads.Count > 0;

    [ObservableProperty]
    private bool _hasDownloadedFiles;

    [ObservableProperty]
    private string _currentDownloadFolderPath = string.Empty;

    public string WallpaperCountText => LocalWallpapers.Count == 1
        ? "1 wallpaper"
        : $"{LocalWallpapers.Count} wallpapers";

    public DownloadsViewModel(IDownloadService downloadService, ILocalStorageService storageService, IWallpaperService wallpaperService)
    {
        _downloadService = downloadService;
        _storageService = storageService;
        _wallpaperService = wallpaperService;

        _downloadService.Downloads.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasActiveDownloads));

        // Fire-and-forget: kick off initial background scan on startup
        _ = RefreshDownloads();
    }

    [RelayCommand]
    public async Task RefreshDownloads()
    {
        // Prevent concurrent refreshes
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            string[] extensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

            // Run ALL file system operations on a thread pool thread to avoid UI lag
            var (foundFiles, primaryFolder) = await Task.Run(() =>
            {
                var foldersToScan = new List<string>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Storage service configured download directory
                try
                {
                    string configuredFolder = _storageService.GetDownloadDirectory();
                    if (!string.IsNullOrWhiteSpace(configuredFolder) && Directory.Exists(configuredFolder))
                        foldersToScan.Add(configuredFolder);
                }
                catch { }

                // 2. Settings direct folder path if set
                try
                {
                    var settingsFolder = _storageService.LoadSettings().DownloadFolderPath;
                    if (!string.IsNullOrWhiteSpace(settingsFolder) && Directory.Exists(settingsFolder)
                        && !foldersToScan.Contains(settingsFolder, StringComparer.OrdinalIgnoreCase))
                        foldersToScan.Add(settingsFolder);
                }
                catch { }

                // 3. Fallback appdata downloads folder
                string appDataDownloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk", "Downloads");
                if (Directory.Exists(appDataDownloads)
                    && !foldersToScan.Contains(appDataDownloads, StringComparer.OrdinalIgnoreCase))
                    foldersToScan.Add(appDataDownloads);

                var result = new List<FileInfo>();
                foreach (var folder in foldersToScan)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(folder);
                        foreach (var file in dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories))
                        {
                            if (extensions.Contains(file.Extension.ToLowerInvariant()) && seenPaths.Add(file.FullName))
                                result.Add(file);
                        }
                    }
                    catch
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(folder);
                            foreach (var file in dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
                            {
                                if (extensions.Contains(file.Extension.ToLowerInvariant()) && seenPaths.Add(file.FullName))
                                    result.Add(file);
                            }
                        }
                        catch { }
                    }
                }

                return (result.OrderByDescending(f => f.CreationTime).ToList(), foldersToScan.FirstOrDefault() ?? string.Empty);
            });

            // Back on UI thread — batch update collection
            LocalWallpapers.Clear();
            foreach (var file in foundFiles)
            {
                LocalWallpapers.Add(new LocalWallpaperItem
                {
                    FilePath = file.FullName,
                    FileSize = file.Length,
                    CreatedAt = file.CreationTime
                });
            }

            HasDownloadedFiles = LocalWallpapers.Count > 0;
            OnPropertyChanged(nameof(WallpaperCountText));
            CurrentDownloadFolderPath = primaryFolder;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task SetAsWallpaper(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            await _wallpaperService.SetWallpaperAsync(filePath, -1, WallpaperFit.Fill);
        }
    }

    [RelayCommand]
    private async Task SetAsLockScreen(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            await _wallpaperService.SetLockScreenAsync(filePath);
        }
    }

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        string folder = _storageService.GetDownloadDirectory();
        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void OpenFile(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void DeleteFile(LocalWallpaperItem? item)
    {
        if (item != null && File.Exists(item.FilePath))
        {
            try
            {
                File.Delete(item.FilePath);
                LocalWallpapers.Remove(item);
                HasDownloadedFiles = LocalWallpapers.Count > 0;
            }
            catch { }
        }
    }
}

public class LocalWallpaperItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FormattedFileSize
    {
        get
        {
            if (FileSize >= 1024 * 1024)
                return $"{(double)FileSize / (1024 * 1024):F1} MB";
            if (FileSize >= 1024)
                return $"{(double)FileSize / 1024:F0} KB";
            return $"{FileSize} B";
        }
    }

    public string FormattedDate => CreatedAt.ToString("MMM d, yyyy");
}
