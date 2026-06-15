namespace Richie.Application.Common;

/// <summary>
/// The central Richie brand palette (adapted from the design system). Values are sRGB hex strings so
/// any layer can convert to its own colour type (WPF <c>Media.Color</c>, SkiaSharp <c>SKColor</c>).
/// The categorical list is colour-blind-safe and is the single source of truth for chart series
/// colours on-screen and in exported reports. No "Others" bucket — every series is named explicitly.
/// </summary>
public static class BrandColors
{
    public const string Primary = "#BE3A2F";   // Richie Red
    public const string Secondary = "#D89A45"; // Golden Amber
    public const string Accent = "#E2BE74";    // Soft Gold

    // Status triad (consistent app-wide): green = good, amber = needs attention, red = critical.
    public const string Success = "#1FA56C";
    public const string Warning = "#DE9326";
    public const string Danger = "#CE2E20";

    /// <summary>Distinct, colour-blind-safe categorical series colours.</summary>
    public static readonly IReadOnlyList<string> Categorical =
    [
        "#BE3A2F", // Richie Red
        "#D89A45", // Gold
        "#4FA87A", // Emerald
        "#3E86C6", // Blue
        "#CC7DAC", // Rose
        "#6E59A5", // Violet
        "#2A9D8F", // Teal
        "#B5651D", // Sienna
        "#8E7CC3", // Lavender
        "#5F8B4C"  // Olive
    ];
}
