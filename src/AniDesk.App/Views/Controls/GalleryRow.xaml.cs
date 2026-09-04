using System.Windows;
using System.Windows.Controls;
using AniDesk.App.ViewModels;
using AniDesk.Core.Models;

namespace AniDesk.App.Views.Controls;

public partial class GalleryRow : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(PostRow), typeof(GalleryRow),
            new PropertyMetadata(null, OnRowChanged));

    public PostRow? Row
    {
        get => (PostRow?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public GalleryRow()
    {
        InitializeComponent();
    }

    private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GalleryRow gr)
            gr.Rebuild();
    }

    private void Rebuild()
    {
        var row = Row;
        if (row == null)
        {
            RowGrid.Children.Clear();
            return;
        }

        int targetCount = row.Items.Count;
        RowGrid.Columns = targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            var post = row.Items[i];

            if (i < RowGrid.Children.Count)
            {
                var existing = RowGrid.Children[i];

                if (post == null)
                {
                    if (existing is Border)
                    {
                        existing.Visibility = Visibility.Visible;
                        continue;
                    }
                    else
                    {
                        RowGrid.Children[i] = new Border { Margin = new Thickness(4) };
                        continue;
                    }
                }
                else
                {
                    if (existing is WallpaperCard card)
                    {
                        card.DataContext = post;
                        card.Visibility = Visibility.Visible;
                        continue;
                    }
                    else
                    {
                        RowGrid.Children[i] = new WallpaperCard { DataContext = post };
                        continue;
                    }
                }
            }

            // Append new if row expanded
            if (post == null)
            {
                RowGrid.Children.Add(new Border { Margin = new Thickness(4) });
            }
            else
            {
                RowGrid.Children.Add(new WallpaperCard { DataContext = post });
            }
        }

        // Trim excess children if column count decreased
        while (RowGrid.Children.Count > targetCount)
        {
            RowGrid.Children.RemoveAt(RowGrid.Children.Count - 1);
        }
    }
}
