using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Richie.UI.Converters;

/// <summary>Colours a profit/loss value specifically for the Asset Documentation page: teal when ≥ 0, orange when negative.</summary>
public sealed class AssetPageProfitLossBrushConverter : IValueConverter
{
    private static readonly Brush Teal = Freeze(Color.FromRgb(0x0F, 0x76, 0x6E));
    private static readonly Brush Orange = Freeze(Color.FromRgb(0xEA, 0x58, 0x0C));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double n = value switch
        {
            decimal d => (double)d,
            double db => db,
            float f => f,
            int i => i,
            _ => 0
        };
        return n < 0 ? Orange : Teal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
