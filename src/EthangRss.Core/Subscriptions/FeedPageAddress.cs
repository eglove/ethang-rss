using EthangRss.Core.YouTube;

namespace EthangRss.Core.Subscriptions;

/// <summary>
/// The page a person would visit for a stored feed. A subscription is kept under the address the
/// app fetches, and that is never what a browser should be opened on: a channel is stored under
/// the app's own scheme, and a feed document is a machine's address rather than the site it
/// belongs to. Both are turned into the page a reader would have gone to by hand.
/// </summary>
public static class FeedPageAddress
{
    /// <summary>The page to open, or null when neither address names one a browser can load.</summary>
    public static string? For(string? xmlAddress, string? website)
    {
        if (ChannelAddress.IsChannelAddress(xmlAddress))
        {
            return ChannelAddress.ChannelIdFrom(xmlAddress) is { } channelId
                ? "https://www.youtube.com/channel/" + channelId
                : null;
        }

        // The site the feed belongs to is what a person means by the feed's page; the feed document
        // itself is only the fallback, for feeds that declare no site at all.
        return WebPage(website) ?? WebPage(xmlAddress);
    }

    private static string? WebPage(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? address
            : null;
}