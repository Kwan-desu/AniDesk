using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class FavoritesViewModel : ObservableObject
{
    private readonly ILocalStorageService _storageService;

    [ObservableProperty]
    private ObservableCollection<MoebooruPost> _favorites = new();

    [ObservableProperty]
    private bool _isEmpty;

    public event EventHandler<MoebooruPost>? PostSelected;

    public FavoritesViewModel(ILocalStorageService storageService)
    {
        _storageService = storageService;
        LoadFavorites();
    }

    public void LoadFavorites()
    {
        Favorites.Clear();
        var list = _storageService.LoadFavorites();
        foreach (var post in list)
        {
            post.IsFavorite = true;
            Favorites.Add(post);
        }
        IsEmpty = Favorites.Count == 0;
    }

    [RelayCommand]
    private void RemoveFavorite(MoebooruPost? post)
    {
        if (post == null) return;
        _storageService.RemoveFavorite(post.Id);
        Favorites.Remove(post);
        IsEmpty = Favorites.Count == 0;
    }

    [RelayCommand]
    private void SelectPost(MoebooruPost? post)
    {
        if (post == null) return;
        PostSelected?.Invoke(this, post);
    }
}
