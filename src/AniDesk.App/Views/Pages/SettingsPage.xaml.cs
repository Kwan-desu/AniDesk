using System.IO;
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
    private readonly PanicButtonService? _panicService;

    private bool _isLoadingSettings;

    public SettingsPage()
    {
        InitializeComponent();

        _storageService = App.Services.GetRequiredService<ILocalStorageService>();
        _safetyService = App.Services.GetRequiredService<IContentSafetyService>();
        _cacheService = App.Services.GetRequiredService<IImageCacheService>();
        _panicService = App.Services.GetService<PanicButtonService>();

        Loaded += (s, e) =>
        {
            LoadSettings();
            DefaultWallpaperRadio.Checked += OnDefaultWallpaperChecked;
            CustomWallpaperRadio.Checked += OnCustomWallpaperChecked;
        };
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            var settings = _storageService.LoadSettings();
            SfwSwitch.IsChecked = settings.IsSfwShieldActive;
            DownloadPathBox.Text = _storageService.GetDownloadDirectory();
            TraySwitch.IsChecked = settings.MinimizeToTrayOnClose;
            EnablePanicSwitch.IsChecked = settings.EnableEmergencyDesktop;
            StartupSwitch.IsChecked = settings.RunPanicDaemonOnStartup;

            if (!string.IsNullOrWhiteSpace(settings.PanicWallpaperPath) && File.Exists(settings.PanicWallpaperPath))
            {
                CustomWallpaperRadio.IsChecked = true;
                if (CustomPathGrid != null) CustomPathGrid.Visibility = Visibility.Visible;
                PanicWallpaperPathBox.Text = settings.PanicWallpaperPath;
            }
            else
            {
                DefaultWallpaperRadio.IsChecked = true;
                if (CustomPathGrid != null) CustomPathGrid.Visibility = Visibility.Collapsed;
                PanicWallpaperPathBox.Text = string.Empty;
            }

            UpdateCacheSizeText();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void UpdateCacheSizeText()
    {
        if (_cacheService == null) return;
        long bytes = _cacheService.GetCacheSizeInBytes();
        double mb = (double)bytes / (1024 * 1024);
        CacheSizeText.Text = $"Currently cached: {mb:F2} MB";
    }

    private void OnSfwToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded || _safetyService == null) return;
        if (SfwSwitch.IsChecked.HasValue)
        {
            _safetyService.IsSfwShieldActive = SfwSwitch.IsChecked.Value;
        }
    }

    private void OnTrayToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded || _storageService == null) return;
        if (TraySwitch.IsChecked.HasValue)
        {
            var settings = _storageService.LoadSettings();
            settings.MinimizeToTrayOnClose = TraySwitch.IsChecked.Value;
            _storageService.SaveSettings(settings);
        }
    }

    private void OnEnablePanicToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded || _storageService == null) return;
        if (EnablePanicSwitch.IsChecked.HasValue)
        {
            var settings = _storageService.LoadSettings();
            settings.EnableEmergencyDesktop = EnablePanicSwitch.IsChecked.Value;
            _storageService.SaveSettings(settings);

            if (_panicService != null)
            {
                _panicService.IsEnabled = settings.EnableEmergencyDesktop;
            }
        }
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded || _storageService == null) return;
        if (StartupSwitch.IsChecked.HasValue)
        {
            bool enable = StartupSwitch.IsChecked.Value;
            var settings = _storageService.LoadSettings();
            settings.RunPanicDaemonOnStartup = enable;
            _storageService.SaveSettings(settings);

            try
            {
                const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, true);
                if (key != null)
                {
                    string appPath = Environment.ProcessPath ?? string.Empty;
                    if (enable && !string.IsNullOrWhiteSpace(appPath))
                    {
                        key.SetValue("AniDesk", $"\"{appPath}\" --daemon");
                    }
                    else
                    {
                        key.DeleteValue("AniDesk", false);
                    }
                }
            }
            catch { }
        }
    }

    private void OnDefaultWallpaperChecked(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded || _storageService == null) return;

        if (CustomPathGrid != null)
        {
            CustomPathGrid.Visibility = Visibility.Collapsed;
        }

        var settings = _storageService.LoadSettings();
        settings.PanicWallpaperPath = string.Empty;
        _storageService.SaveSettings(settings);

        _panicService?.SetCustomSafeWallpaper(null);
    }

    private void OnCustomWallpaperChecked(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded || _storageService == null) return;

        if (CustomPathGrid != null)
        {
            CustomPathGrid.Visibility = Visibility.Visible;
        }
    }

    private void OnBrowsePanicWallpaperClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Clean/Safe Wallpaper",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PanicWallpaperPathBox.Text = dialog.FileName;
            var settings = _storageService.LoadSettings();
            settings.PanicWallpaperPath = dialog.FileName;
            _storageService.SaveSettings(settings);

            _panicService?.SetCustomSafeWallpaper(dialog.FileName);
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
