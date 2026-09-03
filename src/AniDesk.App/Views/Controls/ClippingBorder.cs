using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AniDesk.App.Views.Controls;

/// <summary>
/// Specialized Border control that hardware-clips its Child to its CornerRadius.
/// Solves WPF's default behavior where Border.ClipToBounds only clips to rectangular bounds.
/// </summary>
public class ClippingBorder : Border
{
    protected override void OnRender(DrawingContext dc)
    {
        ApplyChildClip();
        base.OnRender(dc);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ApplyChildClip();
    }

    private void ApplyChildClip()
    {
        if (Child != null && ActualWidth > 0 && ActualHeight > 0)
        {
            double radius = Math.Max(0, CornerRadius.TopLeft - Math.Max(BorderThickness.Left, BorderThickness.Top));
            Child.Clip = new RectangleGeometry(
                new Rect(0, 0, ActualWidth, ActualHeight),
                radius,
                radius);
        }
    }
}
