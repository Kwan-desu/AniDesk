using System.Windows.Controls;
using AniDesk.App.ViewModels;

namespace AniDesk.App.Views.Pages;

public partial class DownloadsPage : UserControl
{
    public DownloadsPage()
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            if (DataContext is DownloadsViewModel vm)
            {
                vm.RefreshDownloads();
            }
        };

        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible && DataContext is DownloadsViewModel vm)
            {
                vm.RefreshDownloads();
            }
        };
    }
}
