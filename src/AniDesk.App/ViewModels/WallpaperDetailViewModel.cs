using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Interop;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class WallpaperDetailViewModel : ObservableObject
{
    private readonly IWallpaperService _wallpaperService;
    private readonly IDownloadService _downloadService;
    private readonly ILocalStorageService _storageService;

    [ObservableProperty]
    private MoebooruPost? _post;

    [ObservableProperty]
    private ObservableCollection<DisplayMonitorInfo> _monitors = new();

    [ObservableProperty]
    private DisplayMonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private WallpaperFit _selectedFit = WallpaperFit.Fill;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDownloadingOriginal;

    [ObservableProperty]
    private double _downloadProgressPercent;

    [ObservableProperty]
    private string _activeDownloadId = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusSuccess;

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? WallpaperApplied;

    public WallpaperDetailViewModel(
        IWallpaperService wallpaperService,
        IDownloadService downloadService,
        ILocalStorageService storageService)
    {
        _wallpaperService = wallpaperService;
        _downloadService = downloadService;
        _storageService = storageService;

        _downloadService.DownloadProgressChanged += (s, e) =>
        {
            if (e.Item.Post?.Id == Post?.Id || e.Item.Id == ActiveDownloadId)
            {
                DownloadProgressPercent = Math.Round(e.Progress, 0);
            }
        };

        _downloadService.DownloadCompleted += (s, e) =>
        {
            if (e.Item.Post?.Id == Post?.Id || e.Item.Id == ActiveDownloadId)
            {
                IsDownloadingOriginal = false;
                if (e.Success)
                {
                    StatusMessage = "✓ Download completed!";
                    IsStatusSuccess = true;
                    WallpaperApplied?.Invoke(this, $"Original wallpaper downloaded: {e.Item.FileName}");
                }
                else if (e.ErrorMessage != "Cancelled")
                {
                    StatusMessage = $"⚠ Download failed: {e.ErrorMessage}";
                    IsStatusSuccess = false;
                }
            }
        };

        LoadMonitors();
    }

    public void LoadMonitors()
    {
        Monitors.Clear();
        var detected = _wallpaperService.GetConnectedMonitors();
        foreach (var m in detected)
        {
            Monitors.Add(m);
        }

        SelectedMonitor = Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
    }

    public void SetPost(MoebooruPost? post)
    {
        Post = post;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SetWallpaperAsync()
    {
        if (Post == null || IsBusy) return;

        IsBusy = true;
        StatusMessage = "Applying wallpaper...";
        IsStatusSuccess = false;

        try
        {
            int monitorIndex = SelectedMonitor?.Index ?? -1;
            string targetUrl = !string.IsNullOrWhiteSpace(Post.SampleUrl) ? Post.SampleUrl : Post.BestImageUrl;
            bool success = await _wallpaperService.SetWallpaperAsync(targetUrl, monitorIndex, SelectedFit);

            if (success)
            {
                StatusMessage = "✓ Wallpaper applied!";
                IsStatusSuccess = true;
                WallpaperApplied?.Invoke(this, $"Wallpaper applied to {SelectedMonitor?.DisplayName ?? "all displays"}!");
            }
            else
            {
                StatusMessage = "⚠ Failed to apply wallpaper.";
                IsStatusSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠ Error: {ex.Message}";
            IsStatusSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetLockScreenAsync()
    {
        if (Post == null || IsBusy) return;

        IsBusy = true;
        StatusMessage = "Setting lock screen...";
        IsStatusSuccess = false;

        try
        {
            string targetUrl = !string.IsNullOrWhiteSpace(Post.SampleUrl) ? Post.SampleUrl : Post.BestImageUrl;
            bool success = await _wallpaperService.SetLockScreenAsync(targetUrl);
            if (success)
            {
                StatusMessage = "✓ Lock screen updated!";
                IsStatusSuccess = true;
                WallpaperApplied?.Invoke(this, "Lock screen wallpaper updated successfully!");
            }
            else
            {
                StatusMessage = "⚠ Failed to update lock screen.";
                IsStatusSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsStatusSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadOriginalAsync()
    {
        if (Post == null || IsDownloadingOriginal) return;

        IsDownloadingOriginal = true;
        DownloadProgressPercent = 0;
        StatusMessage = "Starting download in background...";
        IsStatusSuccess = true;

        try
        {
            var item = await _downloadService.DownloadPostAsync(Post);
            ActiveDownloadId = item.Id;
            StatusMessage = "Downloading in background... (0%)";
        }
        catch (Exception ex)
        {
            IsDownloadingOriginal = false;
            StatusMessage = $"Download error: {ex.Message}";
            IsStatusSuccess = false;
        }
    }

    [RelayCommand]
    private void CancelCurrentDownload()
    {
        if (!string.IsNullOrWhiteSpace(ActiveDownloadId))
        {
            _downloadService.CancelDownload(ActiveDownloadId);
            IsDownloadingOriginal = false;
            StatusMessage = "Download cancelled.";
        }
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Post == null) return;

        if (Post.IsFavorite)
        {
            _storageService.RemoveFavorite(Post.Id);
            Post.IsFavorite = false;
        }
        else
        {
            _storageService.AddFavorite(Post);
            Post.IsFavorite = true;
        }

        OnPropertyChanged(nameof(Post));
    }

    [RelayCommand]
    private void OpenSourceUrl()
    {
        if (Post == null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Post.SourceWebUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
