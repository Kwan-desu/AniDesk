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
        string folder = _storageService.GetDownloadDirectory();
        if (Directory.Exists(folder))
        {
            string[] extensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp"];
            try
            {
                var files = new DirectoryInfo(folder)
                    .GetFiles()
                    .Where(f => extensions.Contains(f.Extension.ToLowerInvariant()))
                    .OrderByDescending(f => f.CreationTime);

                foreach (var file in files)
                {
                    LocalWallpapers.Add(new LocalWallpaperItem
                    {
                        FilePath = file.FullName,
                        FileSize = file.Length,
                        CreatedAt = file.CreationTime
                    });
                }
            }
            catch { }
        }
        HasDownloadedFiles = LocalWallpapers.Count > 0;
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
