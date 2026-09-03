using System.Windows;
using System.Windows.Controls;
using AniDesk.App.ViewModels;

namespace AniDesk.App.Views.Pages;

public partial class FavoritesPage : UserControl
{
    public FavoritesPage()
    {
        InitializeComponent();
    }

    private void OnFavoritesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is FavoritesViewModel vm && e.NewSize.Width > 0)
        {
            vm.UpdateColumns(e.NewSize.Width);
        }
    }
}
