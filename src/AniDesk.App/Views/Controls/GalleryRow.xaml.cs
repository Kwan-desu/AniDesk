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
        RowGrid.Children.Clear();

        var row = Row;
        if (row == null) return;

        RowGrid.Columns = row.Items.Count;

        foreach (var post in row.Items)
        {
            if (post == null)
            {
                // Empty placeholder to keep grid geometry correct
                RowGrid.Children.Add(new Border { Margin = new Thickness(4) });
            }
            else
            {
                var card = new WallpaperCard { DataContext = post };
                RowGrid.Children.Add(card);
            }
        }
    }
}
