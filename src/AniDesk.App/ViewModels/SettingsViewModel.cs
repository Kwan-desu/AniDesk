using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalStorageService _storageService;
    private readonly IContentSafetyService _safetyService;
    private readonly IImageCacheService _cacheService;
    private readonly PanicButtonService? _panicService;

    [ObservableProperty]
    private bool _isSfwShieldActive;

    [ObservableProperty]
    private string _downloadPath = string.Empty;

    [ObservableProperty]
    private string _cacheSizeText = "0.00 MB";

    [ObservableProperty]
    private string _panicWallpaperPath = string.Empty;

    [ObservableProperty]
    private bool _minimizeToTrayOnClose = true;

    [ObservableProperty]
    private string _panicHotkeyDisplay = "Win + Shift + H";

    [ObservableProperty]
    private bool _isCustomPanicWallpaper;

    public SettingsViewModel(
        ILocalStorageService storageService,
        IContentSafetyService safetyService,
        IImageCacheService cacheService,
        PanicButtonService? panicService = null)
    {
        _storageService = storageService;
        _safetyService = safetyService;
        _cacheService = cacheService;
        _panicService = panicService;

        LoadSettings();
    }

    public void LoadSettings()
    {
        var settings = _storageService.LoadSettings();
        IsSfwShieldActive = settings.IsSfwShieldActive;
        DownloadPath = _storageService.GetDownloadDirectory();
        PanicWallpaperPath = settings.PanicWallpaperPath;
        MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        PanicHotkeyDisplay = string.IsNullOrWhiteSpace(settings.PanicHotkeyDisplay) ? "Win + Shift + H" : settings.PanicHotkeyDisplay;
        IsCustomPanicWallpaper = !string.IsNullOrWhiteSpace(PanicWallpaperPath);
        UpdateCacheSizeText();
    }

    public void UpdateCacheSizeText()
    {
        long bytes = _cacheService.GetCacheSizeInBytes();
        double mb = (double)bytes / (1024 * 1024);
        CacheSizeText = $"{mb:F2} MB";
    }

    partial void OnIsSfwShieldActiveChanged(bool value)
    {
        _safetyService.IsSfwShieldActive = value;
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        var settings = _storageService.LoadSettings();
        settings.MinimizeToTrayOnClose = value;
        _storageService.SaveSettings(settings);
    }

    [RelayCommand]
    private void BrowseDownloadPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Download Directory",
            InitialDirectory = DownloadPath
        };

        if (dialog.ShowDialog() == true)
        {
            var settings = _storageService.LoadSettings();
            settings.DownloadFolderPath = dialog.FolderName;
            _storageService.SaveSettings(settings);
            DownloadPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowsePanicWallpaper()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Clean/Safe Wallpaper",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PanicWallpaperPath = dialog.FileName;
            IsCustomPanicWallpaper = true;
            _panicService?.SetCustomSafeWallpaper(PanicWallpaperPath);

            var settings = _storageService.LoadSettings();
            settings.PanicWallpaperPath = PanicWallpaperPath;
            _storageService.SaveSettings(settings);
        }
    }

    [RelayCommand]
    private void ResetPanicWallpaper()
    {
        PanicWallpaperPath = string.Empty;
        IsCustomPanicWallpaper = false;
        _panicService?.SetCustomSafeWallpaper(null);

        var settings = _storageService.LoadSettings();
        settings.PanicWallpaperPath = string.Empty;
        _storageService.SaveSettings(settings);
    }

    [RelayCommand]
    private void ClearCache()
    {
        _cacheService.ClearCache();
        UpdateCacheSizeText();
    }
}
