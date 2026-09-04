using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Services;
using AniDesk.Core.Models;

namespace AniDesk.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalStorageService _storageService;
    private readonly IContentSafetyService _safetyService;
    private readonly IImageCacheService _cacheService;
    private readonly PanicButtonService? _panicService;
    private readonly IDynamicWallpaperService? _dynamicService;

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
    private bool _startMinimizedToTray;

    [ObservableProperty]
    private string _panicHotkeyDisplay = "Win + Shift + H";

    [ObservableProperty]
    private string _selectedPanicModifier = "Win + Shift";

    [ObservableProperty]
    private string _selectedPanicKey = "H";

    [ObservableProperty]
    private bool _isCustomPanicWallpaper;

    // Dynamic Wallpaper Properties
    [ObservableProperty]
    private bool _enableDynamicWallpaper;

    [ObservableProperty]
    private int _dynamicWallpaperIntervalMinutes = 5;

    [ObservableProperty]
    private DynamicWallpaperSource _selectedDynamicSource = DynamicWallpaperSource.Favorites;

    [ObservableProperty]
    private bool _dynamicShuffle = true;

    public string[] AvailablePanicModifiers { get; } =
    [
        "Win + Shift",
        "Win + Ctrl",
        "Ctrl + Shift",
        "Ctrl + Alt",
        "Alt + Shift"
    ];

    public string[] AvailablePanicKeys { get; } =
    [
        "H", "P", "D", "B", "Q", "X", "Z",
        "F1", "F2", "F3", "F4", "F8", "F9", "F10", "F11", "F12",
        "Escape", "Tilde (~)"
    ];

    public DynamicWallpaperSource[] AvailableDynamicSources { get; } =
    [
        DynamicWallpaperSource.Favorites,
        DynamicWallpaperSource.Downloads,
        DynamicWallpaperSource.Both
    ];

    public SettingsViewModel(
        ILocalStorageService storageService,
        IContentSafetyService safetyService,
        IImageCacheService cacheService,
        PanicButtonService? panicService = null,
        IDynamicWallpaperService? dynamicService = null)
    {
        _storageService = storageService;
        _safetyService = safetyService;
        _cacheService = cacheService;
        _panicService = panicService;
        _dynamicService = dynamicService;

        LoadSettings();
    }

    public void LoadSettings()
    {
        var settings = _storageService.LoadSettings();
        IsSfwShieldActive = settings.IsSfwShieldActive;
        DownloadPath = _storageService.GetDownloadDirectory();
        PanicWallpaperPath = settings.PanicWallpaperPath;
        MinimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
        StartMinimizedToTray = settings.StartMinimizedToTray;
        PanicHotkeyDisplay = string.IsNullOrWhiteSpace(settings.PanicHotkeyDisplay) ? "Win + Shift + H" : settings.PanicHotkeyDisplay;
        IsCustomPanicWallpaper = !string.IsNullOrWhiteSpace(PanicWallpaperPath);

        // Parse modifier and key
        ParseHotkeyDisplay(PanicHotkeyDisplay);

        // Dynamic Wallpaper
        EnableDynamicWallpaper = settings.EnableDynamicWallpaper;
        DynamicWallpaperIntervalMinutes = Math.Clamp(settings.DynamicWallpaperIntervalMinutes, 1, 1440);
        SelectedDynamicSource = settings.DynamicSource;
        DynamicShuffle = settings.DynamicShuffle;

        UpdateCacheSizeText();
    }

    private void ParseHotkeyDisplay(string display)
    {
        var parts = display.Split('+').Select(p => p.Trim()).ToList();
        if (parts.Count >= 2)
        {
            string key = parts[^1];
            string mod = string.Join(" + ", parts.Take(parts.Count - 1));
            if (AvailablePanicModifiers.Contains(mod)) SelectedPanicModifier = mod;
            if (AvailablePanicKeys.Contains(key)) SelectedPanicKey = key;
        }
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

    partial void OnStartMinimizedToTrayChanged(bool value)
    {
        var settings = _storageService.LoadSettings();
        settings.StartMinimizedToTray = value;
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

    [RelayCommand]
    public void UpdateCustomHotkey((uint Modifiers, uint VirtualKey, string Display) hotkeyInfo)
    {
        UpdateCustomHotkey(hotkeyInfo.Modifiers, hotkeyInfo.VirtualKey, hotkeyInfo.Display);
    }

    public void UpdateCustomHotkey(uint modifiers, uint virtualKey, string display)
    {
        PanicHotkeyDisplay = display;

        var settings = _storageService.LoadSettings();
        settings.PanicHotkeyDisplay = display;
        settings.PanicModifiers = modifiers;
        settings.PanicKey = virtualKey;
        _storageService.SaveSettings(settings);

        _panicService?.UpdateHotkey(modifiers, virtualKey);
    }

    [RelayCommand]
    public void ApplyCustomHotkey()
    {
        uint mod = SelectedPanicModifier switch
        {
            "Win + Ctrl" => AniDesk.Core.Interop.NativeMethods.MOD_WIN | AniDesk.Core.Interop.NativeMethods.MOD_CONTROL | AniDesk.Core.Interop.NativeMethods.MOD_NOREPEAT,
            "Ctrl + Shift" => AniDesk.Core.Interop.NativeMethods.MOD_CONTROL | AniDesk.Core.Interop.NativeMethods.MOD_SHIFT | AniDesk.Core.Interop.NativeMethods.MOD_NOREPEAT,
            "Ctrl + Alt" => AniDesk.Core.Interop.NativeMethods.MOD_CONTROL | AniDesk.Core.Interop.NativeMethods.MOD_ALT | AniDesk.Core.Interop.NativeMethods.MOD_NOREPEAT,
            "Alt + Shift" => AniDesk.Core.Interop.NativeMethods.MOD_ALT | AniDesk.Core.Interop.NativeMethods.MOD_SHIFT | AniDesk.Core.Interop.NativeMethods.MOD_NOREPEAT,
            _ => AniDesk.Core.Interop.NativeMethods.MOD_WIN | AniDesk.Core.Interop.NativeMethods.MOD_SHIFT | AniDesk.Core.Interop.NativeMethods.MOD_NOREPEAT
        };

        uint vk = SelectedPanicKey switch
        {
            "P" => 0x50,
            "D" => 0x44,
            "B" => 0x42,
            "Q" => 0x51,
            "X" => 0x58,
            "Z" => 0x5A,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            "Escape" => 0x1B,
            "Tilde (~)" => 0xC0,
            _ => 0x48 // 'H'
        };

        string display = $"{SelectedPanicModifier} + {SelectedPanicKey}";
        UpdateCustomHotkey(mod, vk, display);
    }

    [RelayCommand]
    public void SaveDynamicWallpaperSettings()
    {
        var settings = _storageService.LoadSettings();
        settings.EnableDynamicWallpaper = EnableDynamicWallpaper;
        settings.DynamicWallpaperIntervalMinutes = Math.Clamp(DynamicWallpaperIntervalMinutes, 1, 1440);
        settings.DynamicSource = SelectedDynamicSource;
        settings.DynamicShuffle = DynamicShuffle;
        _storageService.SaveSettings(settings);

        _dynamicService?.UpdateSettings(settings);
    }

    [RelayCommand]
    public async Task ShuffleNextWallpaper()
    {
        if (_dynamicService != null)
        {
            await _dynamicService.TriggerNextAsync();
        }
    }
}
