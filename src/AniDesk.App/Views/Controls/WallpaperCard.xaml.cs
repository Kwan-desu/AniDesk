using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using AniDesk.App.ViewModels;
using AniDesk.Core.Models;
using AniDesk.Core.Services;

namespace AniDesk.App.Views.Controls;

public partial class WallpaperCard : UserControl
{
    public WallpaperCard()
    {
        InitializeComponent();
    }

    private void OnCardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCardHeight(e.NewSize.Width);
    }

    private void OnCardDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateCardHeight(ActualWidth);
    }

    private void UpdateCardHeight(double width)
    {
        if (width > 20 && DataContext is MoebooruPost post && post.Width > 0 && post.Height > 0)
        {
            double ratio = (double)post.Width / post.Height;
            if (ratio <= 0.01) ratio = 16.0 / 9.0;
            double desiredHeight = Math.Clamp(Math.Round(width / ratio), 90.0, 360.0);
            if (Math.Abs(Height - desiredHeight) > 1)
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
                mainVm.OpenPostDetail(post);
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

    private async void OnQuickSetClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is MoebooruPost post)
        {
            var wallpaperService = App.Services.GetService<IWallpaperService>();
            if (wallpaperService != null)
            {
                string targetUrl = !string.IsNullOrEmpty(post.SampleUrl) ? post.SampleUrl : post.FileUrl;
                await wallpaperService.SetWallpaperAsync(targetUrl, -1, WallpaperFit.Fill);
            }
        }
    }

    private async void OnContextMenuSetWallpaper(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoebooruPost post)
        {
            var wallpaperService = App.Services.GetService<IWallpaperService>();
            if (wallpaperService != null)
            {
                string targetUrl = !string.IsNullOrEmpty(post.SampleUrl) ? post.SampleUrl : post.FileUrl;
                await wallpaperService.SetWallpaperAsync(targetUrl, -1, WallpaperFit.Fill);
            }
        }
    }

    private async void OnContextMenuSetLockScreen(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoebooruPost post)
        {
            var wallpaperService = App.Services.GetService<IWallpaperService>();
            if (wallpaperService != null)
            {
                string targetUrl = !string.IsNullOrEmpty(post.SampleUrl) ? post.SampleUrl : post.FileUrl;
                await wallpaperService.SetLockScreenAsync(targetUrl);
            }
        }
    }

    private void OnContextMenuToggleFavorite(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoebooruPost post)
        {
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.ExploreVM.ToggleFavoriteCommand.Execute(post);
            }
        }
    }

    private void OnContextMenuDownload(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoebooruPost post)
        {
            var downloadService = App.Services.GetService<IDownloadService>();
            if (downloadService != null)
            {
                _ = downloadService.DownloadPostAsync(post);
            }
        }
    }

    private void OnContextMenuCopyUrl(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoebooruPost post)
        {
            try
            {
                Clipboard.SetText(post.FileUrl);
            }
            catch { }
        }
    }
}
