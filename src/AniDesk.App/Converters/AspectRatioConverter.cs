using System;
using System.Globalization;
using System.Windows.Data;

namespace AniDesk.App.Converters;

public class AspectRatioConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3)
        {
            return 170.0;
        }

        if (values[0] is not double containerWidth || containerWidth <= 20)
        {
            return 170.0;
        }

        double imgW = 0;
        double imgH = 0;

        if (values[1] is int wInt) imgW = wInt;
        else if (values[1] is double wDouble) imgW = wDouble;

        if (values[2] is int hInt) imgH = hInt;
        else if (values[2] is double hDouble) imgH = hDouble;

        if (imgW <= 0 || imgH <= 0)
        {
            return Math.Round(containerWidth * 9.0 / 16.0);
        }

        double ratio = imgW / imgH;
        if (ratio <= 0.01) ratio = 16.0 / 9.0;

        double calculatedHeight = containerWidth / ratio;

        // Clamp to visually pleasing desktop bounds:
        // Min 90px (very wide 32:9 banners) to Max 360px (tall portrait/posters)
        return Math.Clamp(Math.Round(calculatedHeight), 90.0, 360.0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
