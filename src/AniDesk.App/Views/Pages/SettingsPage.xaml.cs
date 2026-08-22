using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using AniDesk.Core.Services;

namespace AniDesk.App.Views.Pages;

public partial class SettingsPage : UserControl
{
    private readonly ILocalStorageService _storageService;
    private readonly IContentSafetyService _safetyService;
    private readonly IImageCacheService _cacheService;

    public SettingsPage()
    {
        InitializeComponent();

        _storageService = App.Services.GetRequiredService<ILocalStorageService>();
        _safetyService = App.Services.GetRequiredService<IContentSafetyService>();
        _cacheService = App.Services.GetRequiredService<IImageCacheService>();

        Loaded += (s, e) => LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _storageService.LoadSettings();
        SfwSwitch.IsChecked = settings.IsSfwShieldActive;
        DownloadPathBox.Text = _storageService.GetDownloadDirectory();
        UpdateCacheSizeText();
    }

    private void UpdateCacheSizeText()
    {
        long bytes = _cacheService.GetCacheSizeInBytes();
        double mb = (double)bytes / (1024 * 1024);
        CacheSizeText.Text = $"Currently cached: {mb:F2} MB";
    }

    private void OnSfwToggled(object sender, RoutedEventArgs e)
    {
        if (SfwSwitch.IsChecked.HasValue)
        {
            _safetyService.IsSfwShieldActive = SfwSwitch.IsChecked.Value;
        }
    }

    private void OnBrowseClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Download Directory",
            InitialDirectory = _storageService.GetDownloadDirectory()
        };

        if (dialog.ShowDialog() == true)
        {
            var settings = _storageService.LoadSettings();
            settings.DownloadFolderPath = dialog.FolderName;
            _storageService.SaveSettings(settings);
            DownloadPathBox.Text = dialog.FolderName;
        }
    }

    private void OnClearCacheClicked(object sender, RoutedEventArgs e)
    {
        _cacheService.ClearCache();
        UpdateCacheSizeText();
    }
}
