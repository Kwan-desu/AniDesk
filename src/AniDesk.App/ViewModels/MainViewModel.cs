using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IContentSafetyService _safetyService;
    private readonly ILocalStorageService _storageService;

    public ExploreViewModel ExploreVM { get; }
    public FavoritesViewModel FavoritesVM { get; }
    public DownloadsViewModel DownloadsVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public WallpaperDetailViewModel DetailVM { get; }

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private string _currentNavView = "Explore";

    [ObservableProperty]
    private bool _isDetailPanelOpen = false;

    [ObservableProperty]
    private bool _isSfwShieldActive;

    [ObservableProperty]
    private string _themeAccent = "default";

    public MainViewModel(
        ExploreViewModel exploreVM,
        FavoritesViewModel favoritesVM,
        DownloadsViewModel downloadsVM,
        SettingsViewModel settingsVM,
        WallpaperDetailViewModel detailVM,
        IContentSafetyService safetyService,
        ILocalStorageService storageService)
    {
        ExploreVM = exploreVM;
        FavoritesVM = favoritesVM;
        DownloadsVM = downloadsVM;
        SettingsVM = settingsVM;
        DetailVM = detailVM;
        _safetyService = safetyService;
        _storageService = storageService;

        _currentViewModel = exploreVM;
        _isSfwShieldActive = _safetyService.IsSfwShieldActive;
        _themeAccent = _storageService.LoadSettings().ThemeAccent;

        // Wire up selection events to detail view model
        ExploreVM.PostSelected += (s, post) =>
        {
            DetailVM.SetPost(post);
            IsDetailPanelOpen = true;
        };

        FavoritesVM.PostSelected += (s, post) =>
        {
            DetailVM.SetPost(post);
            IsDetailPanelOpen = true;
        };

        DetailVM.CloseRequested += (s, e) =>
        {
            IsDetailPanelOpen = false;
        };

        _safetyService.SafetyStateChanged += (s, active) =>
        {
            if (IsSfwShieldActive != active)
            {
                IsSfwShieldActive = active;
            }
        };
    }

    partial void OnIsSfwShieldActiveChanged(bool value)
    {
        if (_safetyService.IsSfwShieldActive != value)
        {
            _safetyService.IsSfwShieldActive = value;
            _ = ExploreVM.SearchAsync();
        }
    }

    [RelayCommand]
    private void Navigate(string viewName)
    {
        CurrentNavView = viewName;
        switch (viewName)
        {
            case "Explore":
                CurrentViewModel = ExploreVM;
                break;
            case "Popular":
                ExploreVM.SearchTags = "order:score";
                _ = ExploreVM.SearchAsync();
                CurrentViewModel = ExploreVM;
                CurrentNavView = "Explore";
                break;
            case "Favorites":
                FavoritesVM.LoadFavorites();
                CurrentViewModel = FavoritesVM;
                break;
            case "Downloads":
                CurrentViewModel = DownloadsVM;
                break;
            case "Settings":
                SettingsVM.LoadSettings();
                CurrentViewModel = SettingsVM;
                break;
            default:
                CurrentViewModel = ExploreVM;
                break;
        }
    }

    [RelayCommand]
    private void ToggleDetailPanel()
    {
        IsDetailPanelOpen = !IsDetailPanelOpen;
    }

    [RelayCommand]
    public void SetThemeAccent(string accent)
    {
        ThemeAccent = accent;
        var settings = _storageService.LoadSettings();
        settings.ThemeAccent = accent;
        _storageService.SaveSettings(settings);
    }
}
