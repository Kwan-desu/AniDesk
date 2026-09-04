using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public interface IContentSafetyService
{
    bool IsSfwShieldActive { get; set; }
    string PrepareTagsQuery(string rawTags);
    IEnumerable<MoebooruPost> FilterPosts(IEnumerable<MoebooruPost> posts);
    bool IsPostAllowed(MoebooruPost post);
    event EventHandler<bool>? SafetyStateChanged;
}

public class ContentSafetyService : IContentSafetyService
{
    private readonly ILocalStorageService _storageService;
    private bool _isSfwShieldActive;

    public event EventHandler<bool>? SafetyStateChanged;

    public bool IsSfwShieldActive
    {
        get => _isSfwShieldActive;
        set
        {
            if (_isSfwShieldActive != value)
            {
                _isSfwShieldActive = value;
                var settings = _storageService.LoadSettings();
                settings.IsSfwShieldActive = value;
                _storageService.SaveSettings(settings);
                SafetyStateChanged?.Invoke(this, value);
            }
        }
    }

    public ContentSafetyService(ILocalStorageService storageService)
    {
        _storageService = storageService;
        _isSfwShieldActive = _storageService.LoadSettings().IsSfwShieldActive;
    }

    public string PrepareTagsQuery(string rawTags)
    {
        string trimmed = (rawTags ?? string.Empty).Trim();

        if (IsSfwShieldActive)
        {
            // Remove any manual rating tags
            var tagParts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !t.StartsWith("rating:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Inject safe rating tag
            tagParts.Add("rating:s");
            return string.Join(" ", tagParts);
        }

        return trimmed;
    }

    public IEnumerable<MoebooruPost> FilterPosts(IEnumerable<MoebooruPost> posts)
    {
        if (!IsSfwShieldActive)
        {
            return posts;
        }

        // Strict client-side safety guard: drop non-safe items
        return posts.Where(IsPostAllowed);
    }

    public bool IsPostAllowed(MoebooruPost post)
    {
        if (!IsSfwShieldActive)
        {
            return true;
        }

        return string.Equals(post.Rating, "s", StringComparison.OrdinalIgnoreCase);
    }
}
