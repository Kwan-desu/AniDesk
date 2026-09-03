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
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private bool _isCinematicOpen = false;

    [ObservableProperty]
    private MoebooruPost? _cinematicPost;

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

        // Wire up selection events to open cinematic theater overlay
        ExploreVM.PostSelected += (s, post) =>
        {
            OpenPostDetail(post);
        };

        FavoritesVM.PostSelected += (s, post) =>
        {
            OpenPostDetail(post);
        };

        DetailVM.CloseRequested += (s, e) =>
        {
            IsDetailPanelOpen = false;
            IsCinematicOpen = false;
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
                ExploreVM.IsPopularMode = false;
                CurrentViewModel = ExploreVM;
                _ = ExploreVM.SearchAsync();
                break;
            case "Popular":
                ExploreVM.IsPopularMode = true;
                CurrentViewModel = ExploreVM;
                _ = ExploreVM.SearchAsync();
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

    public void OpenPostDetail(MoebooruPost post)
    {
        DetailVM.SetPost(post);
        CinematicPost = post;
        IsDetailPanelOpen = true; // Opens the right-side preview drawer with options!
        IsCinematicOpen = false;
    }

    [RelayCommand]
    public void OpenCinematicFromDetail()
    {
        if (DetailVM.Post != null)
        {
            CinematicPost = DetailVM.Post;
            IsCinematicOpen = true;
        }
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    [RelayCommand]
    public void CloseCinematic()
    {
        IsCinematicOpen = false;
    }

    [RelayCommand]
    public void ToggleSfw()
    {
        IsSfwShieldActive = !IsSfwShieldActive;
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
