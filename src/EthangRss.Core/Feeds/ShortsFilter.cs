using System.Text.RegularExpressions;

namespace EthangRss.Core.Feeds;

/// <summary>Identifies YouTube Shorts links, which are dropped rather than stored.</summary>
public static partial class ShortsFilter
{
    public static bool IsShorts(string link)
    {
        return !string.IsNullOrWhiteSpace(link) && ShortsPattern().IsMatch(link);
    }

    [GeneratedRegex(@"^https?://(www\.)?youtube\.com/shorts/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShortsPattern();
}
