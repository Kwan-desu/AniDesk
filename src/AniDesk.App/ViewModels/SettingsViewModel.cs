using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalStorageService _storageService;
    private readonly IContentSafetyService _safetyService;
    private readonly IImageCacheService _cacheService;

    [ObservableProperty]
    private bool _isSfwShieldActive;

    [ObservableProperty]
    private string _downloadPath = string.Empty;

    [ObservableProperty]
    private string _cacheSizeText = "0.00 MB";

    public SettingsViewModel(
        ILocalStorageService storageService,
        IContentSafetyService safetyService,
        IImageCacheService cacheService)
    {
        _storageService = storageService;
        _safetyService = safetyService;
        _cacheService = cacheService;

        LoadSettings();
    }

    public void LoadSettings()
    {
        var settings = _storageService.LoadSettings();
        IsSfwShieldActive = settings.IsSfwShieldActive;
        DownloadPath = _storageService.GetDownloadDirectory();
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
    private void ClearCache()
    {
        _cacheService.ClearCache();
        UpdateCacheSizeText();
    }
}
