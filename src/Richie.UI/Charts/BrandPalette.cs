using LiveChartsCore.SkiaSharpView.Painting;
using Richie.Application.Common;
using SkiaSharp;

namespace Richie.UI.Charts;

/// <summary>
/// Brand chart colours for on-screen LiveCharts series, backed by <see cref="BrandColors"/> so the
/// in-app charts match the exported report charts. Categorical for pies (one colour per slice),
/// solid brand colours for single-series columns/lines.
/// </summary>
public static class BrandPalette
{
    public static readonly SKColor[] Colors = BrandColors.Categorical.Select(SKColor.Parse).ToArray();
    public static readonly SKColor Primary = SKColor.Parse(BrandColors.Primary);
    public static readonly SKColor Success = SKColor.Parse(BrandColors.Success);
    public static readonly SKColor Warning = SKColor.Parse(BrandColors.Warning);
    public static readonly SKColor Danger = SKColor.Parse(BrandColors.Danger);

    public static SKColor At(int index) => Colors[index % Colors.Length];

    /// <summary>A fresh paint for the categorical colour at <paramref name="index"/> (wraps around).</summary>
    public static SolidColorPaint Categorical(int index) => new(At(index));

    /// <summary>A fresh paint for a specific brand colour.</summary>
    public static SolidColorPaint Solid(SKColor color) => new(color);
}
