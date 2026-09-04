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

        RefreshDownloads();
    }

    [RelayCommand]
    public void RefreshDownloads()
    {
        LocalWallpapers.Clear();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] extensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

        var foldersToScan = new List<string>();

        // 1. Storage service configured download directory
        string configuredFolder = _storageService.GetDownloadDirectory();
        if (!string.IsNullOrWhiteSpace(configuredFolder) && Directory.Exists(configuredFolder))
        {
            foldersToScan.Add(configuredFolder);
        }

        // 2. Settings direct folder path if set
        try
        {
            var settingsFolder = _storageService.LoadSettings().DownloadFolderPath;
            if (!string.IsNullOrWhiteSpace(settingsFolder) && Directory.Exists(settingsFolder) && !foldersToScan.Contains(settingsFolder, StringComparer.OrdinalIgnoreCase))
            {
                foldersToScan.Add(settingsFolder);
            }
        }
        catch { }

        // 3. Fallback appdata downloads folder (%LocalAppData%\AniDesk\Downloads)
        string appDataDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk", "Downloads");
        if (Directory.Exists(appDataDownloads) && !foldersToScan.Contains(appDataDownloads, StringComparer.OrdinalIgnoreCase))
        {
            foldersToScan.Add(appDataDownloads);
        }

        var foundFiles = new List<FileInfo>();

        foreach (var folder in foldersToScan)
        {
            try
            {
                var dirInfo = new DirectoryInfo(folder);
                var files = dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(f.Extension.ToLowerInvariant()));

                foreach (var file in files)
                {
                    if (seenPaths.Add(file.FullName))
                    {
                        foundFiles.Add(file);
                    }
                }
            }
            catch
            {
                // In case AllDirectories encounters an unauthorized subfolder, fallback to top-directory
                try
                {
                    var dirInfo = new DirectoryInfo(folder);
                    var files = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => extensions.Contains(f.Extension.ToLowerInvariant()));

                    foreach (var file in files)
                    {
                        if (seenPaths.Add(file.FullName))
                        {
                            foundFiles.Add(file);
                        }
                    }
                }
                catch { }
            }
        }

        foreach (var file in foundFiles.OrderByDescending(f => f.CreationTime))
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

        // Update the displayed folder path
        CurrentDownloadFolderPath = foldersToScan.FirstOrDefault() ?? string.Empty;
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
