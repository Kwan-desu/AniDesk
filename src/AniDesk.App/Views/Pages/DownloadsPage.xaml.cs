using System.Windows.Controls;
using AniDesk.App.ViewModels;

namespace AniDesk.App.Views.Pages;

public partial class DownloadsPage : UserControl
{
    public DownloadsPage()
    {
        InitializeComponent();

        // Refresh downloads whenever this page becomes visible (user navigates to Downloads)
        // The ViewModel already does an initial scan in its constructor.
        IsVisibleChanged += async (s, e) =>
        {
            if (IsVisible && DataContext is DownloadsViewModel vm)
            {
                await vm.RefreshDownloads();
            }
        };
    }
}
