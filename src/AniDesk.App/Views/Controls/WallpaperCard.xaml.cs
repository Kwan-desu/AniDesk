using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AniDesk.App.ViewModels;
using AniDesk.Core.Models;

namespace AniDesk.App.Views.Controls;

public partial class WallpaperCard : UserControl
{
    public WallpaperCard()
    {
        InitializeComponent();
    }

    private void OnCardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 20)
        {
            double desiredHeight = Math.Round(e.NewSize.Width * 9.0 / 16.0);
            if (desiredHeight > 60 && Math.Abs(Height - desiredHeight) > 1)
            {
                Height = desiredHeight;
            }
        }
    }

    private void OnCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MoebooruPost post)
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.ExploreVM.SelectPostCommand.Execute(post);
                mainVm.FavoritesVM.SelectPostCommand.Execute(post);
            }
        }
    }

    private void OnFavoriteClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MoebooruPost post)
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.ExploreVM.ToggleFavoriteCommand.Execute(post);
            }
        }
    }
}
