using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
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

    public bool HasQueueItems => Downloads.Count > 0;
    public bool HasActiveDownloads => Downloads.Any(d => d.Status is DownloadStatus.Downloading or DownloadStatus.Queued);
    public bool HasCompletedDownloads => Downloads.Any(d => d.Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled);

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

        _downloadService.Downloads.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasQueueItems));
            OnPropertyChanged(nameof(HasActiveDownloads));
            OnPropertyChanged(nameof(HasCompletedDownloads));
        };

        _downloadService.DownloadCompleted += OnDownloadCompleted;
        _storageService.DownloadDirectoryChanged += OnDownloadDirectoryChanged;

        // Kick off initial scan
        _ = RefreshDownloads();
    }

    private void OnDownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        if (!e.Success || string.IsNullOrWhiteSpace(e.Item?.TargetFilePath)) return;

        var app = Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
        {
            app.Dispatcher.BeginInvoke(() =>
            {
                var existing = LocalWallpapers.FirstOrDefault(w => string.Equals(w.FilePath, e.Item.TargetFilePath, StringComparison.OrdinalIgnoreCase));
                if (existing == null && File.Exists(e.Item.TargetFilePath))
                {
                    var fi = new FileInfo(e.Item.TargetFilePath);
                    LocalWallpapers.Insert(0, new LocalWallpaperItem
                    {
                        FilePath = fi.FullName,
                        FileSize = fi.Length,
                        CreatedAt = fi.CreationTime
                    });
                    HasDownloadedFiles = true;
                    OnPropertyChanged(nameof(WallpaperCountText));
                }
                OnPropertyChanged(nameof(HasQueueItems));
                OnPropertyChanged(nameof(HasActiveDownloads));
                OnPropertyChanged(nameof(HasCompletedDownloads));
            });
        }
    }

    private void OnDownloadDirectoryChanged(object? sender, string newDirectory)
    {
        CurrentDownloadFolderPath = newDirectory;
        _ = RefreshDownloads();
    }

    [RelayCommand]
    public async Task RefreshDownloads()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            string[] extensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

            var (foundFiles, primaryFolder) = await Task.Run(() =>
            {
                var foldersToScan = new List<string>();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Configured folder
                try
                {
                    string configuredFolder = _storageService.GetDownloadDirectory();
                    if (!string.IsNullOrWhiteSpace(configuredFolder) && Directory.Exists(configuredFolder))
                        foldersToScan.Add(configuredFolder);
                }
                catch { }

                // 2. Settings direct path if distinct
                try
                {
                    var settingsFolder = _storageService.LoadSettings().DownloadFolderPath;
                    if (!string.IsNullOrWhiteSpace(settingsFolder) && Directory.Exists(settingsFolder)
                        && !foldersToScan.Contains(settingsFolder, StringComparer.OrdinalIgnoreCase))
                        foldersToScan.Add(settingsFolder);
                }
                catch { }

                // 3. AppData downloads folder
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

            // Incremental differential reconciliation — preserve existing items and loaded previews
            var foundMap = new Dictionary<string, FileInfo>(foundFiles.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var file in foundFiles)
            {
                foundMap[file.FullName] = file;
            }

            // Remove deleted files (iterate backwards)
            for (int i = LocalWallpapers.Count - 1; i >= 0; i--)
            {
                if (!foundMap.ContainsKey(LocalWallpapers[i].FilePath))
                {
                    LocalWallpapers.RemoveAt(i);
                }
            }

            var existingPaths = new HashSet<string>(LocalWallpapers.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var item in LocalWallpapers)
            {
                existingPaths.Add(item.FilePath);
            }

            // Insert newly discovered files (sorted newest first)
            int insertIndex = 0;
            foreach (var file in foundFiles)
            {
                if (!existingPaths.Contains(file.FullName))
                {
                    LocalWallpapers.Insert(insertIndex, new LocalWallpaperItem
                    {
                        FilePath = file.FullName,
                        FileSize = file.Length,
                        CreatedAt = file.CreationTime
                    });
                }
                insertIndex++;
            }

            HasDownloadedFiles = LocalWallpapers.Count > 0;
            OnPropertyChanged(nameof(WallpaperCountText));
            CurrentDownloadFolderPath = primaryFolder;
            OnPropertyChanged(nameof(HasQueueItems));
            OnPropertyChanged(nameof(HasActiveDownloads));
            OnPropertyChanged(nameof(HasCompletedDownloads));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    [RelayCommand]
    private void ClearCompletedDownloads()
    {
        _downloadService.ClearCompleted();
        OnPropertyChanged(nameof(HasQueueItems));
        OnPropertyChanged(nameof(HasActiveDownloads));
        OnPropertyChanged(nameof(HasCompletedDownloads));
    }

    [RelayCommand]
    private void CancelDownload(DownloadItem? item)
    {
        if (item != null)
        {
            _downloadService.CancelDownload(item.Id);
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
                OnPropertyChanged(nameof(WallpaperCountText));
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

    public string FormatBadge
    {
        get
        {
            string ext = Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
            return string.IsNullOrWhiteSpace(ext) ? "IMG" : ext;
        }
    }

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
