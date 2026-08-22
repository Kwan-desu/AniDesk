using System.Text.Json;
using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public interface ILocalStorageService
{
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
    List<MoebooruPost> LoadFavorites();
    void SaveFavorites(IEnumerable<MoebooruPost> favorites);
    bool AddFavorite(MoebooruPost post);
    bool RemoveFavorite(long postId);
    bool IsFavorite(long postId);
    string GetDownloadDirectory();
}

public class LocalStorageService : ILocalStorageService
{
    private readonly string _appDataFolder;
    private readonly string _settingsFile;
    private readonly string _favoritesFile;
    private readonly object _lock = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public LocalStorageService()
    {
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniDesk"
        );

        Directory.CreateDirectory(_appDataFolder);
        _settingsFile = Path.Combine(_appDataFolder, "settings.json");
        _favoritesFile = Path.Combine(_appDataFolder, "favorites.json");
    }

    public AppSettings LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = File.ReadAllText(_settingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                    if (settings != null)
                    {
                        if (string.IsNullOrWhiteSpace(settings.DownloadFolderPath))
                        {
                            settings.DownloadFolderPath = GetDefaultDownloadFolder();
                        }
                        return settings;
                    }
                }
            }
            catch
            {
                // Fallback to default
            }

            var defaultSettings = new AppSettings
            {
                DownloadFolderPath = GetDefaultDownloadFolder()
            };
            SaveSettings(defaultSettings);
            return defaultSettings;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_settingsFile, json);
            }
            catch
            {
                // Ignore transient write errors
            }
        }
    }

    public List<MoebooruPost> LoadFavorites()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_favoritesFile))
                {
                    string json = File.ReadAllText(_favoritesFile);
                    var list = JsonSerializer.Deserialize<List<MoebooruPost>>(json, _jsonOptions);
                    if (list != null)
                    {
                        foreach (var post in list)
                        {
                            post.IsFavorite = true;
                        }
                        return list;
                    }
                }
            }
            catch
            {
                // Return empty on failure
            }

            return new List<MoebooruPost>();
        }
    }

    public void SaveFavorites(IEnumerable<MoebooruPost> favorites)
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(favorites.ToList(), _jsonOptions);
                File.WriteAllText(_favoritesFile, json);
            }
            catch
            {
                // Ignore transient write error
            }
        }
    }

    public bool AddFavorite(MoebooruPost post)
    {
        lock (_lock)
        {
            var list = LoadFavorites();
            if (list.Any(p => p.Id == post.Id && p.SourceProvider == post.SourceProvider))
            {
                return false;
            }

            post.IsFavorite = true;
            list.Insert(0, post);
            SaveFavorites(list);
            return true;
        }
    }

    public bool RemoveFavorite(long postId)
    {
        lock (_lock)
        {
            var list = LoadFavorites();
            int removed = list.RemoveAll(p => p.Id == postId);
            if (removed > 0)
            {
                SaveFavorites(list);
                return true;
            }
            return false;
        }
    }

    public bool IsFavorite(long postId)
    {
        lock (_lock)
        {
            var list = LoadFavorites();
            return list.Any(p => p.Id == postId);
        }
    }

    public string GetDownloadDirectory()
    {
        var settings = LoadSettings();
        if (!string.IsNullOrWhiteSpace(settings.DownloadFolderPath) && Directory.Exists(settings.DownloadFolderPath))
        {
            return settings.DownloadFolderPath;
        }

        string defaultFolder = GetDefaultDownloadFolder();
        Directory.CreateDirectory(defaultFolder);
        return defaultFolder;
    }

    private static string GetDefaultDownloadFolder()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(pictures))
        {
            pictures = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        return Path.Combine(pictures, "AniDesk Wallpapers");
    }
}
