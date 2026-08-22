using Wpf.Ui.Controls;
using AniDesk.App.ViewModels;

namespace AniDesk.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
