namespace AniDesk.Core.Models;

public enum BooruSource
{
    All,
    Yandere,
    KonachanNet,   // konachan.net  — SFW only
    KonachanCom    // konachan.com  — allows explicit (NSFW)
}

public enum ContentRating
{
    Safe,
    Questionable,
    Explicit,
    All
}

public enum WallpaperFit
{
    Fill = 0,
    Fit = 1,
    Stretch = 2,
    Tile = 3,
    Center = 4,
    Span = 5
}

public enum DownloadStatus
{
    Queued,
    Downloading,
    Completed,
    Failed
}
