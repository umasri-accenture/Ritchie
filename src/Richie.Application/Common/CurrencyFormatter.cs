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
}
