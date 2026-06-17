using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Richie.UI.Converters;

/// <summary>Colours a profit/loss value for the Asset page: green when ≥ 0, light orange when negative (consistent app-wide palette).</summary>
public sealed class AssetPageProfitLossBrushConverter : IValueConverter
{
    private static readonly Brush Green = Freeze(Color.FromRgb(0x57, 0xB8, 0x94));
    private static readonly Brush Orange = Freeze(Color.FromRgb(0xFF, 0xB3, 0x66));

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
        return n < 0 ? Orange : Green;
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
