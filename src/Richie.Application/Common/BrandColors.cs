namespace Richie.Application.Common;

/// <summary>
/// The central Richie brand palette (adapted from the design system). Values are sRGB hex strings so
/// any layer can convert to its own colour type (WPF <c>Media.Color</c>, SkiaSharp <c>SKColor</c>).
/// The categorical list is colour-blind-safe and is the single source of truth for chart series
/// colours on-screen and in exported reports. No "Others" bucket — every series is named explicitly.
/// </summary>
public static class BrandColors
{
    public const string Primary = "#2926c9";   // Richie Red
    public const string Secondary = "#45a5d8"; // Golden Amber
    public const string Accent = "#fbfaf7";    // Soft Gold

    // Status triad (consistent app-wide): green = good, amber = needs attention, red = critical.
    // Updated to remove green/red while keeping a clear status distinction.
    public const string Success = "#25eb3f"; // blue
    public const string Warning = "#DE9326";
    public const string Danger = "#ed523a"; // violet


    // Reserved exclusively for profit (green) / loss (red) in exported reports. By design NO chart
    // series or other report component uses these two colours, so they stay semantically unambiguous.
    public const string ProfitGreen = "#1FA56C";
    public const string LossRed = "#CE2E20";

    /// <summary>Series colours for report charts — deliberately free of green and red hues so the
    /// reserved profit/loss colours are never confused with a chart slice. No "Others" bucket.</summary>
    public static readonly IReadOnlyList<string> ReportChartPalette =
    [
        "#D89A45", // Gold
        "#3E86C6", // Blue
        "#6E59A5", // Violet
        "#CC7DAC", // Rose
        "#B5651D", // Sienna
        "#8E7CC3", // Lavender
        "#2A7DB1", // Ocean
        "#E2BE74", // Soft gold
        "#94527A", // Plum
        "#5A6B8C"  // Slate
    ];

    /// <summary>Distinct, colour-blind-safe categorical series colours for asset allocation charts.</summary>
    public static readonly IReadOnlyList<string> Categorical =
    [
        "#80dbf4", // Richie Red
        "#D89A45", // Gold
        "#52042a", // Emerald
        "#3E86C6", // Blue
        "#CC7DAC", // Rose
        "#6E59A5", // Violet
        "#109787", // Teal
        "#B5651D", // Sienna
        "#8E7CC3", // Lavender
        "#bcca25"  // Olive
    ];
}
