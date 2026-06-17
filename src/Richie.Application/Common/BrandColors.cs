namespace Richie.Application.Common;

/// <summary>
/// The central Richie brand palette (adapted from the design system). Values are sRGB hex strings so
/// any layer can convert to its own colour type (WPF <c>Media.Color</c>, SkiaSharp <c>SKColor</c>).
/// The categorical list is colour-blind-safe and is the single source of truth for chart series
/// colours on-screen and in exported reports. No "Others" bucket — every series is named explicitly.
/// </summary>
public static class BrandColors
{
    public const string Primary = "#E6A756";   // Soft Golden Orange
    public const string Secondary = "#E6A756"; // Soft Golden Orange
    public const string Accent = "#E6A756";    // Soft Golden Orange

    // Status triad (consistent app-wide): green = good, orange shades for all warning/critical states.
    public const string Success = "#57B894";      // Green
    public const string Warning = "#E6A756";      // Medium Orange
    public const string Danger = "#FFB366";       // Light Orange (instead of red)

    /// <summary>Distinct, colour-blind-safe categorical series colours for asset allocation charts.</summary>
    public static readonly IReadOnlyList<string> Categorical =
    [
        "#5B8DEF", // Equity: Blue
        "#E6A756", // Mutual Fund: Orange
        "#57B894", // SGB: Green
        "#9B7EDE", // Digital Gold: Purple
        "#56B7B1", // Real Estate: Teal
        "#F3C969", // Jewellery: Yellow
        "#8A8A8A"  // Guaranteed Plans: Gray
    ];
}
