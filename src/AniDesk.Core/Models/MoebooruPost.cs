using System.Text.Json.Serialization;

namespace AniDesk.Core.Models;

public class MoebooruPost
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("creator_id")]
    public long? CreatorId { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("change")]
    public long? Change { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; } = string.Empty;

    [JsonPropertyName("file_size")]
    public long FileSize { get; set; }

    [JsonPropertyName("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    [JsonPropertyName("is_shown_in_index")]
    public bool? IsShownInIndex { get; set; }

    [JsonPropertyName("preview_url")]
    public string PreviewUrl { get; set; } = string.Empty;

    [JsonPropertyName("preview_width")]
    public int? PreviewWidth { get; set; }

    [JsonPropertyName("preview_height")]
    public int? PreviewHeight { get; set; }

    [JsonPropertyName("actual_preview_width")]
    public int? ActualPreviewWidth { get; set; }

    [JsonPropertyName("actual_preview_height")]
    public int? ActualPreviewHeight { get; set; }

    [JsonPropertyName("sample_url")]
    public string SampleUrl { get; set; } = string.Empty;

    [JsonPropertyName("sample_width")]
    public int? SampleWidth { get; set; }

    [JsonPropertyName("sample_height")]
    public int? SampleHeight { get; set; }

    [JsonPropertyName("sample_file_size")]
    public long? SampleFileSize { get; set; }

    [JsonPropertyName("jpeg_url")]
    public string JpegUrl { get; set; } = string.Empty;

    [JsonPropertyName("jpeg_width")]
    public int? JpegWidth { get; set; }

    [JsonPropertyName("jpeg_height")]
    public int? JpegHeight { get; set; }

    [JsonPropertyName("jpeg_file_size")]
    public long? JpegFileSize { get; set; }

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "s";

    [JsonPropertyName("has_children")]
    public bool? HasChildren { get; set; }

    [JsonPropertyName("parent_id")]
    public long? ParentId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("is_held")]
    public bool? IsHeld { get; set; }

    [JsonPropertyName("frames_pending_string")]
    public string FramesPendingString { get; set; } = string.Empty;

    [JsonPropertyName("frames_string")]
    public string FramesString { get; set; } = string.Empty;

    // Client-side properties
    [JsonIgnore]
    public BooruSource SourceProvider { get; set; } = BooruSource.Yandere;

    [JsonIgnore]
    public bool IsFavorite { get; set; }

    [JsonIgnore]
    public bool IsSafe => string.Equals(Rating, "s", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string BestImageUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FileUrl)) return NormalizeUrl(FileUrl);
            if (!string.IsNullOrWhiteSpace(JpegUrl)) return NormalizeUrl(JpegUrl);
            if (!string.IsNullOrWhiteSpace(SampleUrl)) return NormalizeUrl(SampleUrl);
            return NormalizeUrl(PreviewUrl);
        }
    }

    [JsonIgnore]
    public string ThumbnailUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PreviewUrl)) return NormalizeUrl(PreviewUrl);
            if (!string.IsNullOrWhiteSpace(SampleUrl)) return NormalizeUrl(SampleUrl);
            return BestImageUrl;
        }
    }

    private string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        url = url.Trim();
        if (url.StartsWith("//")) return "https:" + url;
        if (url.StartsWith("/"))
        {
            string host = SourceProvider switch
            {
                BooruSource.KonachanNet => "https://konachan.net",
                BooruSource.KonachanCom => "https://konachan.com",
                _ => "https://yande.re"
            };
            return host + url;
        }
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + url;
        }
        return url;
    }

    [JsonIgnore]
    public string FormattedDimensions => $"{Width} × {Height}";

    [JsonIgnore]
    public string AspectSummary
    {
        get
        {
            if (Height <= 0) return $"{Width} × {Height}";
            double ratio = (double)Width / Height;
            string ratioLabel;
            if (Math.Abs(ratio - (16.0 / 9.0)) < 0.08) ratioLabel = "16:9";
            else if (Math.Abs(ratio - (21.0 / 9.0)) < 0.15) ratioLabel = "21:9 Ultrawide";
            else if (Math.Abs(ratio - (32.0 / 9.0)) < 0.2) ratioLabel = "32:9 Super Ultrawide";
            else if (Math.Abs(ratio - (16.0 / 10.0)) < 0.05) ratioLabel = "16:10";
            else if (Math.Abs(ratio - (4.0 / 3.0)) < 0.05) ratioLabel = "4:3";
            else ratioLabel = ratio > 1 ? "Landscape" : "Portrait";

            string quality = "";
            if (Width >= 3840 || Height >= 2160) quality = " • 4K UHD";
            else if (Width >= 2560 || Height >= 1440) quality = " • 2K QHD";
            else if (Width >= 1920 || Height >= 1080) quality = " • FHD";

            return $"{FormattedDimensions} ({ratioLabel}{quality})";
        }
    }

    [JsonIgnore]
    public string FormattedFileSize
    {
        get
        {
            double bytes = FileSize > 0 ? FileSize : (SampleFileSize ?? 0);
            if (bytes <= 0) return "Unknown size";
            if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024):F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024:F1} KB";
            return $"{bytes} B";
        }
    }

    [JsonIgnore]
    public string[] TagList => string.IsNullOrWhiteSpace(Tags)
        ? Array.Empty<string>()
        : Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [JsonIgnore]
    public string SourceWebUrl
    {
        get
        {
            return SourceProvider switch
            {
                BooruSource.KonachanNet => $"https://konachan.net/post/show/{Id}",
                BooruSource.KonachanCom => $"https://konachan.com/post/show/{Id}",
                _ => $"https://yande.re/post/show/{Id}"
            };
        }
    }
}
