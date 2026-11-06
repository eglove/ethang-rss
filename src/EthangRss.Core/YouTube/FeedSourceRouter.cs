using EthangRss.Core.Feeds;
using EthangRss.Core.Polling;

namespace EthangRss.Core.YouTube;

/// <summary>
/// Sends each address to the source that can serve it. Polling and adding feeds both go through
/// here, so neither has to know that a channel is fetched differently from an HTTP feed.
/// </summary>
public sealed class FeedSourceRouter : IFeedSource
{
    private readonly IFeedSource _feeds;
    private readonly ChannelFetcher _channels;

    public FeedSourceRouter(IFeedSource feeds, ChannelFetcher channels)
    {
        ArgumentNullException.ThrowIfNull(feeds);
        ArgumentNullException.ThrowIfNull(channels);
        _feeds = feeds;
        _channels = channels;
    }

    /// <summary>True when an address names a YouTube channel rather than a feed document.</summary>
    public static bool IsChannel(string? address) => ChannelAddress.IsChannelPage(address);

    public Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken) =>
        IsChannel(xmlAddress)
            ? _channels.FetchAsync(xmlAddress, cancellationToken)
            : _feeds.FetchAsync(xmlAddress, cancellationToken);

    public Task<string?> FetchPageAsync(string address, CancellationToken cancellationToken) =>
        _feeds.FetchPageAsync(address, cancellationToken);
}