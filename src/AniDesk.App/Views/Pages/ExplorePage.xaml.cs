using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AniDesk.App.ViewModels;
using AniDesk.Core.Models;

namespace AniDesk.App.Views.Pages;

public partial class ExplorePage : UserControl
{
    public ExplorePage()
    {
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            UpdateColumnCount();
            if (DataContext is ExploreViewModel vm && vm.PostRows.Count == 0 && !vm.IsLoading)
                await vm.SearchAsync();
        };

        DataContextChanged += async (s, e) =>
        {
            UpdateColumnCount();
            if (DataContext is ExploreViewModel vm && vm.PostRows.Count == 0 && !vm.IsLoading)
                await vm.SearchAsync();
        };
    }

    // ── Responsive columns: update ViewModel.ColumnCount based on gallery width ──
    private void OnGallerySizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateColumnCount();
    }

    private void UpdateColumnCount()
    {
        if (DataContext is not ExploreViewModel vm) return;
        double w = GalleryScrollViewer.ActualWidth;
        int cols = w switch
        {
            >= 1600 => 5,
            >= 1200 => 4,
            >= 800 => 3,
            _ => 2
        };
        if (vm.ColumnCount != cols)
            vm.ColumnCount = cols;
    }

    // ── Search autocomplete keyboard nav ──
    private void OnSearchPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ExploreViewModel vm) return;

        if (vm.IsSuggestionsOpen && vm.TagSuggestions.Count > 0)
        {
            if (e.Key == Key.Down)
            {
                e.Handled = true;
                int i = vm.TagSuggestions.IndexOf(vm.SelectedTagSuggestion ?? vm.TagSuggestions[0]) + 1;
                if (i < vm.TagSuggestions.Count) vm.SelectedTagSuggestion = vm.TagSuggestions[i];
                return;
            }
            if (e.Key == Key.Up)
            {
                e.Handled = true;
                int i = vm.TagSuggestions.IndexOf(vm.SelectedTagSuggestion ?? vm.TagSuggestions[0]) - 1;
                if (i >= 0) vm.SelectedTagSuggestion = vm.TagSuggestions[i];
                return;
            }
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                vm.ApplySuggestion(vm.SelectedTagSuggestion ?? vm.TagSuggestions[0], triggerSearch: false);
                SearchBox.CaretIndex = SearchBox.Text.Length;
                return;
            }
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                vm.ApplySuggestion(vm.SelectedTagSuggestion ?? vm.TagSuggestions[0], triggerSearch: true);
                return;
            }
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.IsSuggestionsOpen = false;
                return;
            }
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = vm.SearchAsync();
        }
    }

    private void OnSuggestionClicked(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExploreViewModel vm && sender is FrameworkElement fe && fe.DataContext is MoebooruTag tag)
        {
            vm.ApplySuggestion(tag, triggerSearch: true);
        }
    }

    // ── Infinite scroll ──
    private void OnGalleryScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight > 0 && e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 400)
        {
            if (DataContext is ExploreViewModel vm)
                _ = vm.LoadMoreAsync();
        }
    }
}
