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
}
