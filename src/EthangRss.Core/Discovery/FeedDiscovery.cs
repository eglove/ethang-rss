using System.Text.RegularExpressions;

namespace EthangRss.Core.Discovery;

/// <summary>
/// Turns a website address into a feed address. Prefers the page's own feed declaration, then
/// tries the conventional paths every blogging platform uses.
/// </summary>
public static partial class FeedDiscovery
{
    private static readonly string[] FeedPaths = ["feed", "rss", "rss.xml", "atom.xml", "feed.xml"];

    public static IReadOnlyList<string> FindFeedUrls(string html, Uri pageAddress)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<string>();
        }

        var found = new List<string>();
        foreach (Match link in LinkTag().Matches(html))
        {
            var tag = link.Value;
            if (!HasFeedType(tag))
            {
                continue;
            }

            var href = Attribute(tag, "href");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var absolute = MakeAbsolute(href, pageAddress);
            if (absolute is not null && !found.Contains(absolute, StringComparer.OrdinalIgnoreCase))
            {
                found.Add(absolute);
            }
        }

        return found;
    }

    public static IReadOnlyList<string> FallbackUrls(Uri pageAddress)
    {
        var root = pageAddress.GetLeftPart(UriPartial.Authority);
        var path = pageAddress.AbsolutePath.EndsWith('/') ? pageAddress.AbsolutePath : pageAddress.AbsolutePath + "/";

        var candidates = FeedPaths.Select(segment => root + path + segment).ToList();
        candidates.AddRange(FeedPaths.Select(segment => root + "/" + segment));

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool LooksLikeFeed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            return false;
        }

        var path = address.AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".xml", StringComparison.Ordinal)
               || path.EndsWith("/feed", StringComparison.Ordinal)
               || path.EndsWith("/rss", StringComparison.Ordinal)
               || path.Contains("/rss/", StringComparison.Ordinal)
               || path.Contains("feed", StringComparison.Ordinal);
    }

    private static bool HasFeedType(string tag)
    {
        var type = Attribute(tag, "type");
        return type is not null
               && (type.Contains("rss", StringComparison.OrdinalIgnoreCase)
                   || type.Contains("atom", StringComparison.OrdinalIgnoreCase));
    }

    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(
            tag,
            "\\s" + name + "\\s*=\\s*(['\"]?)([^'\"\\s>]+)\\1",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups[2].Value : null;
    }

    private static string? MakeAbsolute(string href, Uri pageAddress) =>
        Uri.TryCreate(pageAddress, href, out var absolute) ? absolute.ToString() : null;

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();
}
