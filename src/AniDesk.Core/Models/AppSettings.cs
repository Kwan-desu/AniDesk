namespace AniDesk.Core.Models;

public class AppSettings
{
    public bool IsSfwShieldActive { get; set; } = true;
    public BooruSource DefaultSource { get; set; } = BooruSource.All;
    public string DownloadFolderPath { get; set; } = string.Empty;
    public WallpaperFit DefaultWallpaperFit { get; set; } = WallpaperFit.Fill;
    public int DefaultMonitorIndex { get; set; } = 0;
    public string LastTags { get; set; } = string.Empty;
    public string ThemeAccent { get; set; } = "default";
    public string SelectedAspectRatio { get; set; } = "All";
    public string SelectedMinQuality { get; set; } = "All";
    public string PanicWallpaperPath { get; set; } = string.Empty;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public string PanicHotkeyDisplay { get; set; } = "Win + Shift + H";
    public uint PanicModifiers { get; set; } = 0x0008 | 0x0004 | 0x4000; // MOD_WIN | MOD_SHIFT | MOD_NOREPEAT
    public uint PanicKey { get; set; } = 0x48; // 'H'
    public bool EnableEmergencyDesktop { get; set; } = true;
    public bool RunPanicDaemonOnStartup { get; set; } = false;

    // Dynamic Wallpaper / Slideshow
    public bool EnableDynamicWallpaper { get; set; } = false;
    public int DynamicWallpaperIntervalMinutes { get; set; } = 5;
    public DynamicWallpaperSource DynamicSource { get; set; } = DynamicWallpaperSource.Favorites;
    public bool DynamicShuffle { get; set; } = true;
}
