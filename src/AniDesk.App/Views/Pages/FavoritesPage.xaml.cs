using System.Windows.Controls;
using AniDesk.App.ViewModels;
using AniDesk.Core.Models;

namespace AniDesk.App.Views.Pages;

public partial class FavoritesPage : UserControl
{
    public FavoritesPage()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is MoebooruPost post)
        {
            if (DataContext is FavoritesViewModel vm)
            {
                vm.SelectPostCommand.Execute(post);
            }
        }
    }
}
