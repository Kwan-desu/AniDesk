using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    private ObservableCollection<PostRow> _postRows = new();

    [ObservableProperty]
    private bool _isEmpty;

    private int _columnCount = 4;
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            if (SetProperty(ref _columnCount, value))
            {
                RebuildRows();
            }
        }
    }

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
        RebuildRows();
        IsEmpty = Favorites.Count == 0;
    }

    public void UpdateColumns(double containerWidth)
    {
        int cols = containerWidth switch
        {
            >= 1600 => 5,
            >= 1200 => 4,
            >= 800 => 3,
            _ => 2
        };
        if (cols != ColumnCount)
            ColumnCount = cols;
    }

    private void RebuildRows()
    {
        int cols = Math.Max(1, ColumnCount);
        PostRows.Clear();
        for (int i = 0; i < Favorites.Count; i += cols)
        {
            var items = Enumerable.Range(0, cols)
                .Select(j => i + j < Favorites.Count ? Favorites[i + j] : null);
            PostRows.Add(new PostRow(items));
        }
    }

    [RelayCommand]
    private void RemoveFavorite(MoebooruPost? post)
    {
        if (post == null) return;
        _storageService.RemoveFavorite(post.Id);
        Favorites.Remove(post);
        RebuildRows();
        IsEmpty = Favorites.Count == 0;
    }

    [RelayCommand]
    public void SelectPost(MoebooruPost? post)
    {
        if (post == null) return;
        PostSelected?.Invoke(this, post);
    }
}
