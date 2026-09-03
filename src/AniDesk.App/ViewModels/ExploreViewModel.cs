using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class ExploreViewModel : ObservableObject
{
    private readonly IMoebooruService _moebooruService;
    private readonly IContentSafetyService _safetyService;
    private readonly ILocalStorageService _storageService;
    private readonly IImageCacheService _cacheService;

    private readonly List<MoebooruPost> _allPosts = new();

    [ObservableProperty]
    private ObservableCollection<PostRow> _postRows = new();

    [ObservableProperty]
    private ObservableCollection<MoebooruTag> _tagSuggestions = new();

    [ObservableProperty]
    private bool _isSuggestionsOpen;

    [ObservableProperty]
    private MoebooruTag? _selectedTagSuggestion;

    [ObservableProperty]
    private BooruSource _selectedSource = BooruSource.All;

    [ObservableProperty]
    private string _searchTags = string.Empty;

    [ObservableProperty]
    private bool _isPopularMode;

    [ObservableProperty]
    private string _selectedAspectRatio = "All";

    [ObservableProperty]
    private string _selectedMinQuality = "All";

    [ObservableProperty]
    private bool _isFilterOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private MoebooruPost? _selectedPost;

    /// <summary>Number of columns in the gallery grid — updated by the view on resize.</summary>
    [ObservableProperty]
    private int _columnCount = 4;

    private int _currentPage = 1;
    private const int PageSize = 30;
    private bool _hasMore = true;
    private CancellationTokenSource? _suggestCts;
    private CancellationTokenSource? _searchCts;

    public event EventHandler<MoebooruPost>? PostSelected;

    public ExploreViewModel(
        IMoebooruService moebooruService,
        IContentSafetyService safetyService,
        ILocalStorageService storageService,
        IImageCacheService cacheService)
    {
        _moebooruService = moebooruService;
        _safetyService = safetyService;
        _storageService = storageService;
        _cacheService = cacheService;

        var s = _storageService.LoadSettings();
        _selectedSource = s.DefaultSource;
        _selectedAspectRatio = s.SelectedAspectRatio ?? "All";
        _selectedMinQuality = s.SelectedMinQuality ?? "All";

        _safetyService.SafetyStateChanged += (_, __) => _ = SearchAsync();
    }

    partial void OnSearchTagsChanged(string value) => _ = DebouncedSuggestAsync(value);

    partial void OnColumnCountChanged(int value)
    {
        // Rebuild rows with new column width
        if (_allPosts.Count > 0)
            RebuildRows();
    }

    private async Task DebouncedSuggestAsync(string query)
    {
        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        var tok = _suggestCts.Token;

        string word = LastToken(query);
        if (word.Length < 2) { TagSuggestions.Clear(); IsSuggestionsOpen = false; return; }

        try
        {
            await Task.Delay(160, tok);
            var results = await _moebooruService.GetTagSuggestionsAsync(SelectedSource, word, 8, tok);
            if (tok.IsCancellationRequested) return;
            TagSuggestions.Clear();
            foreach (var t in results) TagSuggestions.Add(t);
            IsSuggestionsOpen = TagSuggestions.Count > 0;
            SelectedTagSuggestion = TagSuggestions.FirstOrDefault();
        }
        catch (OperationCanceledException) { }
        catch { IsSuggestionsOpen = false; }
    }

    public void ApplySuggestion(MoebooruTag? tag, bool triggerSearch = true)
    {
        if (tag == null) return;
        var words = (SearchTags ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count > 0) words[^1] = tag.Name; else words.Add(tag.Name);
        SearchTags = string.Join(" ", words) + " ";
        IsSuggestionsOpen = false;
        TagSuggestions.Clear();
        if (triggerSearch) _ = SearchAsync();
    }

    private static string LastToken(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : string.Empty;
    }

    partial void OnIsPopularModeChanged(bool value)
    {
        if (value)
        {
            SearchTags = string.Empty;
        }
        _ = SearchAsync();
    }

    public string GetEffectiveQueryTags()
    {
        string baseTags = (SearchTags ?? string.Empty).Trim();
        if (IsPopularMode || string.IsNullOrWhiteSpace(baseTags))
        {
            if (string.IsNullOrWhiteSpace(baseTags))
                return "order:score";
            if (!baseTags.Contains("order:", StringComparison.OrdinalIgnoreCase))
                return $"{baseTags} order:score";
        }
        return baseTags;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var tok = _searchCts.Token;

        IsSuggestionsOpen = false;
        _currentPage = 1;
        _hasMore = true;
        _allPosts.Clear();
        PostRows.Clear();
        IsEmpty = false;
        ErrorMessage = string.Empty;
        IsLoading = true;

        try
        {
            var raw = await _moebooruService.GetPostsAsync(SelectedSource, GetEffectiveQueryTags(), 1, PageSize, tok);
            if (tok.IsCancellationRequested) return;

            var filtered = ApplyFilters(raw).ToList();
            foreach (var p in filtered)
            {
                p.IsFavorite = _storageService.IsFavorite(p.Id);
                _allPosts.Add(p);
            }

            RebuildRows();
            IsEmpty = PostRows.Count == 0;

            if (_allPosts.Count > 0 && SelectedPost == null)
                SelectPost(_allPosts[0]);

            _cacheService.PreloadThumbnails(_allPosts.Take(PageSize).Select(p => p.ThumbnailUrl));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ErrorMessage = ex.Message; IsEmpty = true; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoading || IsLoadingMore || !_hasMore) return;
        IsLoadingMore = true;
        _currentPage++;
        var tok = _searchCts?.Token ?? default;
        try
        {
            var raw = await _moebooruService.GetPostsAsync(SelectedSource, GetEffectiveQueryTags(), _currentPage, PageSize, tok);
            if (tok.IsCancellationRequested) return;
            if (raw.Count == 0) { _hasMore = false; return; }
            var ids = _allPosts.Select(p => p.Id).ToHashSet();
            var newPosts = ApplyFilters(raw).Where(p => !ids.Contains(p.Id)).ToList();
            foreach (var p in newPosts)
            {
                p.IsFavorite = _storageService.IsFavorite(p.Id);
                _allPosts.Add(p);
            }
            if (newPosts.Count > 0)
            {
                AppendRows(newPosts.Count);
                _cacheService.PreloadThumbnails(newPosts.Select(p => p.ThumbnailUrl));
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally { IsLoadingMore = false; }
    }

    [RelayCommand]
    public void SelectPost(MoebooruPost? post)
    {
        if (post == null) return;
        SelectedPost = post;
        PostSelected?.Invoke(this, post);
    }

    [RelayCommand]
    private void ToggleFavorite(MoebooruPost? post)
    {
        if (post == null) return;
        if (post.IsFavorite) _storageService.RemoveFavorite(post.Id);
        else _storageService.AddFavorite(post);
        post.IsFavorite = !post.IsFavorite;
    }

    [RelayCommand]
    public async Task ApplyFiltersAsync()
    {
        var s = _storageService.LoadSettings();
        s.DefaultSource = SelectedSource;
        s.SelectedAspectRatio = SelectedAspectRatio;
        s.SelectedMinQuality = SelectedMinQuality;
        _storageService.SaveSettings(s);
        await SearchAsync();
    }

    // ── Row building ──────────────────────────────────────────────

    private void RebuildRows()
    {
        int cols = Math.Max(1, ColumnCount);
        PostRows.Clear();
        for (int i = 0; i < _allPosts.Count; i += cols)
        {
            var items = Enumerable.Range(0, cols)
                .Select(j => i + j < _allPosts.Count ? _allPosts[i + j] : null);
            PostRows.Add(new PostRow(items));
        }
    }

    private void AppendRows(int newPostCount)
    {
        int cols = Math.Max(1, ColumnCount);
        // How many posts were already covered by complete rows
        int coveredByRows = PostRows.Count * cols;

        // Remove any partial last row so we can re-slice cleanly
        if (PostRows.Count > 0)
        {
            var last = PostRows[^1];
            bool partial = last.Items.Any(x => x == null);
            if (partial) PostRows.RemoveAt(PostRows.Count - 1);
        }

        int startIdx = PostRows.Count * cols;
        for (int i = startIdx; i < _allPosts.Count; i += cols)
        {
            var items = Enumerable.Range(0, cols)
                .Select(j => i + j < _allPosts.Count ? _allPosts[i + j] : null);
            PostRows.Add(new PostRow(items));
        }
    }

    private IEnumerable<MoebooruPost> ApplyFilters(IEnumerable<MoebooruPost> items)
    {
        var r = items;
        r = SelectedAspectRatio switch
        {
            "16:9" => r.Where(p => p.Height > 0 && Math.Abs((double)p.Width / p.Height - 16.0 / 9) < 0.12),
            "21:9" => r.Where(p => p.Height > 0 && (double)p.Width / p.Height >= 2.1),
            "Landscape" => r.Where(p => p.Width > p.Height),
            _ => r
        };
        r = SelectedMinQuality switch
        {
            "4K" => r.Where(p => p.Width >= 3840 || p.Height >= 2160),
            "2K" => r.Where(p => p.Width >= 2560 || p.Height >= 1440),
            "FHD" => r.Where(p => p.Width >= 1920 || p.Height >= 1080),
            _ => r
        };
        return r;
    }
}
