using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AniDesk.App.ViewModels;
using AniDesk.Core.Interop;

namespace AniDesk.App.Views.Controls;

public partial class CinematicOverlay : UserControl
{
    private Point _lastDragPoint;
    private bool _isDragging;

    public CinematicOverlay()
    {
        InitializeComponent();
        Loaded += (s, e) => Focus();
        IsVisibleChanged += (s, e) =>
        {
            if (Visibility == Visibility.Visible)
            {
                ResetZoom();
                Focus();
            }
        };
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOverlay();
            e.Handled = true;
        }
    }

    private void OnScrimMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            CloseOverlay();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        if (DataContext is MainViewModel mainVm)
        {
            mainVm.CloseCinematic();
        }
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true; // Prevent gallery scroll bleed

        double delta = e.Delta > 0 ? 1.2 : 0.82;
        double newScale = Math.Clamp(CanvasScale.ScaleX * delta, 1.0, 4.0);

        CanvasScale.ScaleX = newScale;
        CanvasScale.ScaleY = newScale;

        if (newScale <= 1.01)
        {
            CanvasTranslate.X = 0;
            CanvasTranslate.Y = 0;
        }

        ZoomLevelText.Text = $"{(int)(newScale * 100)}%";
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (CanvasScale.ScaleX > 1.01 && sender is IInputElement element)
        {
            _isDragging = true;
            _lastDragPoint = e.GetPosition(this);
            element.CaptureMouse();
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && CanvasScale.ScaleX > 1.01)
        {
            Point current = e.GetPosition(this);
            Vector delta = current - _lastDragPoint;

            CanvasTranslate.X += delta.X;
            CanvasTranslate.Y += delta.Y;

            _lastDragPoint = current;
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && sender is IInputElement element)
        {
            _isDragging = false;
            element.ReleaseMouseCapture();
        }
    }

    private void OnResetZoomClicked(object sender, RoutedEventArgs e)
    {
        ResetZoom();
    }

    private void ResetZoom()
    {
        CanvasScale.ScaleX = 1.0;
        CanvasScale.ScaleY = 1.0;
        CanvasTranslate.X = 0;
        CanvasTranslate.Y = 0;
        if (ZoomLevelText != null)
        {
            ZoomLevelText.Text = "100%";
        }
    }

    private void OnMonitorCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DisplayMonitorInfo monitor)
        {
            if (DataContext is MainViewModel mainVm)
            {
                mainVm.DetailVM.SelectedMonitor = monitor;
            }
        }
    }
}
