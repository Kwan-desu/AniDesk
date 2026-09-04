using System.Net.Http.Json;
using System.Text.Json;
using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public interface IMoebooruService
{
    Task<List<MoebooruPost>> GetPostsAsync(
        BooruSource source, string tags = "", int page = 1, int limit = 30,
        CancellationToken cancellationToken = default);

    Task<List<MoebooruTag>> GetTagSuggestionsAsync(
        BooruSource source, string query, int limit = 8,
        CancellationToken cancellationToken = default);

    string GetBaseUrl(BooruSource source);
}

public class MoebooruService : IMoebooruService
{
    private readonly HttpClient _httpClient;
    private readonly IContentSafetyService _safetyService;
    private readonly SemaphoreSlim _throttle = new(4, 4);

    private readonly Dictionary<string, (DateTime ts, List<MoebooruPost> posts)> _queryCache = new();
    private readonly Dictionary<string, (DateTime ts, List<MoebooruTag> tags)> _tagCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MoebooruService(IContentSafetyService safetyService, HttpClient? httpClient = null)
    {
        _safetyService = safetyService;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            EnableMultipleHttp2Connections = true
        });
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AniDesk/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public string GetBaseUrl(BooruSource source) => source switch
    {
        BooruSource.KonachanNet => "https://konachan.net",
        BooruSource.KonachanCom => "https://konachan.com",
        _ => "https://yande.re"
    };

    public async Task<List<MoebooruTag>> GetTagSuggestionsAsync(
        BooruSource source, string query, int limit = 8,
        CancellationToken cancellationToken = default)
    {
        string q = query.Trim().ToLowerInvariant();
        if (q.Length < 2) return new();

        // Use yande.re for "All" tag suggestions (fastest and most comprehensive)
        var target = source == BooruSource.All ? BooruSource.Yandere : source;
        string key = $"{target}_{q}_{limit}";

        lock (_tagCache)
        {
            if (_tagCache.TryGetValue(key, out var c) && DateTime.UtcNow - c.ts < _cacheDuration)
                return c.tags;
        }

        try
        {
            string url = $"{GetBaseUrl(target)}/tag.json?order=count&name={Uri.EscapeDataString(q)}&limit={limit}";
            var tags = await _httpClient.GetFromJsonAsync<List<MoebooruTag>>(url, _jsonOptions, cancellationToken)
                       ?? new();
            lock (_tagCache) { _tagCache[key] = (DateTime.UtcNow, tags); }
            return tags;
        }
        catch { return new(); }
    }

    public async Task<List<MoebooruPost>> GetPostsAsync(
        BooruSource source, string tags = "", int page = 1, int limit = 30,
        CancellationToken cancellationToken = default)
    {
        if (source == BooruSource.All)
        {
            if (!_safetyService.IsSfwShieldActive)
            {
                // When SFW shield is disabled, fetch directly from yande.re which contains both SFW and NSFW posts
                return await FetchSingleAsync(BooruSource.Yandere, tags, page, limit, cancellationToken);
            }

            // Parallel fetch from yande.re + konachan.net (SFW-safe pair)
            int half = Math.Max(15, limit / 2);
            var t1 = FetchSingleAsync(BooruSource.Yandere, tags, page, half, cancellationToken);
            var t2 = FetchSingleAsync(BooruSource.KonachanNet, tags, page, half, cancellationToken);
            await Task.WhenAll(t1, t2);
            var p1 = await t1;
            var p2 = await t2;
            // Interleave results
            var out_ = new List<MoebooruPost>();
            for (int i = 0; i < Math.Max(p1.Count, p2.Count); i++)
            {
                if (i < p1.Count) out_.Add(p1[i]);
                if (i < p2.Count) out_.Add(p2[i]);
            }
            return out_;
        }
        return await FetchSingleAsync(source, tags, page, limit, cancellationToken);
    }

    private async Task<List<MoebooruPost>> FetchSingleAsync(
        BooruSource source, string tags, int page, int limit,
        CancellationToken ct)
    {
        string prepared = _safetyService.PrepareTagsQuery(tags);
        string key = $"{source}_{prepared}_{page}_{limit}";

        lock (_queryCache)
        {
            if (_queryCache.TryGetValue(key, out var c) && DateTime.UtcNow - c.ts < _cacheDuration)
                return _safetyService.FilterPosts(c.posts).ToList();
        }

        await _throttle.WaitAsync(ct);
        try
        {
            string url = $"{GetBaseUrl(source)}/post.json?page={page}&limit={limit}";
            if (!string.IsNullOrWhiteSpace(prepared))
                url += $"&tags={Uri.EscapeDataString(prepared)}";

            List<MoebooruPost>? posts = null;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var resp = await _httpClient.GetAsync(url, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        posts = await resp.Content.ReadFromJsonAsync<List<MoebooruPost>>(_jsonOptions, ct);
                        break;
                    }
                    else if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && source == BooruSource.KonachanCom)
                    {
                        // Fallback from Cloudflare-protected konachan.com to yande.re
                        string fallbackUrl = $"https://yande.re/post.json?page={page}&limit={limit}";
                        if (!string.IsNullOrWhiteSpace(prepared))
                            fallbackUrl += $"&tags={Uri.EscapeDataString(prepared)}";

                        using var fallbackResp = await _httpClient.GetAsync(fallbackUrl, ct);
                        if (fallbackResp.IsSuccessStatusCode)
                        {
                            posts = await fallbackResp.Content.ReadFromJsonAsync<List<MoebooruPost>>(_jsonOptions, ct);
                            break;
                        }
                    }
                }
                catch when (attempt < 2) { await Task.Delay(250, ct); }
            }

            posts ??= new();
            foreach (var p in posts) p.SourceProvider = source;

            lock (_queryCache) { _queryCache[key] = (DateTime.UtcNow, posts); }
            return _safetyService.FilterPosts(posts).ToList();
        }
        finally { _throttle.Release(); }
    }
}
