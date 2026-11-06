using System.Globalization;

namespace EthangRss.Core.Formatting;

/// <summary>
/// Renders an article timestamp the way the machine's own locale writes dates and times. A fixed
/// pattern would read as American everywhere, so the pattern comes from the culture instead.
/// </summary>
public static class LocaleDate
{
    /// <summary>The general short date and time pattern ("g") for the current culture.</summary>
    public static string Format(DateTimeOffset? value) => Format(value, CultureInfo.CurrentCulture);

    public static string Format(DateTimeOffset? value, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return value is { } timestamp
            ? timestamp.ToLocalTime().ToString("g", culture)
            : string.Empty;
    }
}
