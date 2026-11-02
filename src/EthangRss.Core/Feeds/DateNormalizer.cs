using System.Globalization;

namespace EthangRss.Core.Feeds;

/// <summary>
/// Accepts the date shapes feeds actually publish: RFC 822 / 1123 and ISO 8601, plus the
/// RFC 822 variants that use the informal UT and UTC zone labels.
/// </summary>
public static class DateNormalizer
{
    private static readonly string[] Formats =
    [
        "ddd, dd MMM yyyy HH:mm:ss zzz",
        "ddd, dd MMM yyyy HH:mm zzz",
        "ddd, d MMM yyyy HH:mm:ss zzz",
        "dd MMM yyyy HH:mm:ss zzz",
        "ddd, dd MMM yyyy HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
        "ddd MMM dd HH:mm:ss yyyy",
    ];

    public static DateTimeOffset? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = NormalizeZoneLabels(value.Trim());

        if (DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
        {
            return offset.ToUniversalTime();
        }

        if (DateTimeOffset.TryParseExact(cleaned, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out offset))
        {
            return offset.ToUniversalTime();
        }

        return null;
    }

    private static string NormalizeZoneLabels(string value)
    {
        if (value.EndsWith(" UTC", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(value.AsSpan(0, value.Length - 4), " GMT");
        }

        if (value.EndsWith(" UT", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(value.AsSpan(0, value.Length - 3), " GMT");
        }

        return value;
    }
}
