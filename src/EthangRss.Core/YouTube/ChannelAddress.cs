using System.Text.RegularExpressions;

namespace EthangRss.Core.YouTube;

/// <summary>
/// The address forms a YouTube channel arrives in. Stored feeds use the app's own
/// <c>youtube://channel/&lt;id&gt;</c> scheme so a channel is never mistaken for an HTTP feed;
/// the pasteable forms a person supplies are recognized here too.
/// </summary>
public static partial class ChannelAddress
{
    private const string Scheme = "youtube://channel/";

    /// <summary>The stored address for a channel id.</summary>
    public static string ForChannelId(string channelId) => Scheme + channelId;

    /// <summary>True when the address is already in the app's own channel form.</summary>
    public static bool IsChannelAddress(string? address) =>
        address is not null && address.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The channel id an address carries, or null when it carries none. Handles are not ids:
    /// resolving one needs the network, so that is left to the caller.
    /// </summary>
    public static string? ChannelIdFrom(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        if (IsChannelAddress(address))
        {
            var id = address[Scheme.Length..].Trim('/');
            return IsChannelId(id) ? id : null;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // The dead Atom endpoint still carries the id in its query string, which is what lets
        // existing subscriptions be migrated instead of re-added by hand.
        if (uri.AbsolutePath.Contains("/feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && string.Equals(parts[0], "channel_id", StringComparison.OrdinalIgnoreCase))
                {
                    var queryId = Uri.UnescapeDataString(parts[1]);
                    return IsChannelId(queryId) ? queryId : null;
                }
            }

            return null;
        }

        if (!IsYouTubeHost(uri.Host))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[0] == "channel" && IsChannelId(segments[1])
            ? segments[1]
            : null;
    }

    /// <summary>True when the address points at a channel page, by id or by handle.</summary>
    public static bool IsChannelPage(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (IsChannelAddress(address))
        {
            return ChannelIdFrom(address) is not null;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !IsYouTubeHost(uri.Host))
        {
            return false;
        }

        if (uri.AbsolutePath.Contains("/feeds/videos.xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        return segments[0] == "channel"
            ? segments.Length >= 2 && IsChannelId(segments[1])
            : HandleSegment().IsMatch(segments[0]);
    }

    /// <summary>The address to fetch for a channel page, given what the person supplied.</summary>
    public static string VideosPageFor(string address)
    {
        if (IsChannelAddress(address))
        {
            return "https://www.youtube.com/channel/" + ChannelIdFrom(address) + "/videos";
        }

        var trimmed = address.TrimEnd('/');
        return trimmed.EndsWith("/videos", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "/videos";
    }

    private static bool IsYouTubeHost(string host) =>
        host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("m.youtube.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsChannelId(string? id) => id is { Length: 24 } && ChannelIdPattern().IsMatch(id);

    [GeneratedRegex(@"^UC[A-Za-z0-9_-]{22}$")]
    private static partial Regex ChannelIdPattern();

    [GeneratedRegex(@"^@[A-Za-z0-9._-]{1,60}$")]
    private static partial Regex HandleSegment();
}