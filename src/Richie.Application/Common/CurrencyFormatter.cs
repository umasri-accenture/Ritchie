using System.Globalization;

namespace Richie.Application.Common;

/// <summary>
/// Central money formatter — prefixes the Indian Rupee symbol (₹) and uses the current culture's
/// digit grouping (so it matches XAML bindings that format with <c>₹{0:N2}</c>). Single source of
/// truth so every on-screen amount looks the same.
/// </summary>
public static class CurrencyFormatter
{
    public const string Symbol = "₹";

    /// <summary>₹ with two decimals, e.g. "₹1,530,000.00".</summary>
    public static string Format(decimal value) => Symbol + value.ToString("N2", CultureInfo.CurrentCulture);

    /// <summary>₹ with no decimals, e.g. "₹1,530,000".</summary>
    public static string FormatWhole(decimal value) => Symbol + value.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>Compact format with suffix (K, M, B), e.g. "₹1.5M", "₹20K", "₹1B".</summary>
    public static string FormatCompact(decimal value)
    {
        var absoluteValue = Math.Abs(value);
        string sign = value < 0 ? "-" : "";

        if (absoluteValue >= 1_000_000_000)
        {
            decimal billions = value / 1_000_000_000m;
            return $"{Symbol}{sign}{TrimDecimals(billions)}B";
        }
        else if (absoluteValue >= 1_000_000)
        {
            decimal millions = value / 1_000_000m;
            return $"{Symbol}{sign}{TrimDecimals(millions)}M";
        }
        else if (absoluteValue >= 1_000)
        {
            decimal thousands = value / 1_000m;
            return $"{Symbol}{sign}{TrimDecimals(thousands)}K";
        }
        else
        {
            return $"{Symbol}{sign}{value:0.##}";
        }
    }

    private static string TrimDecimals(decimal value)
    {
        // Format to 1 decimal place, then remove trailing zeros and unnecessary decimal point
        string formatted = value.ToString("0.0", CultureInfo.InvariantCulture);
        return formatted.TrimEnd('0').TrimEnd('.');
    }
}

