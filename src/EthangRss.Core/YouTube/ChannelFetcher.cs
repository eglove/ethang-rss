using System.Net;
using EthangRss.Core.Feeds;

namespace EthangRss.Core.YouTube;

/// <summary>
/// Fetches a channel's videos from its own page. YouTube stopped serving the Atom feed this app
/// was built against, so the page is the source now. Like <see cref="FeedFetcher"/> it never throws
/// for a feed-level problem: an unreadable page is a failure the caller records and shows.
/// </summary>
public sealed class ChannelFetcher
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/131.0.0.0 Safari/537.36";

    private readonly HttpClient _client;
    private readonly Func<DateTimeOffset> _clock;

    public ChannelFetcher(HttpClient client, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<FetchOutcome> FetchAsync(string address, CancellationToken cancellationToken)
    {
        var page = ChannelAddress.VideosPageFor(address);
        if (!Uri.TryCreate(page, UriKind.Absolute, out var uri))
        {
            return FetchOutcome.Failure($"'{address}' is not a channel address.");
        }

        string html;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return FetchOutcome.Failure(
                    $"The channel page responded with {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)response.StatusCode);
            }

            html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FetchOutcome.Failure("The channel page timed out.");
        }
        catch (HttpRequestException exception)
        {
            return FetchOutcome.Failure($"The channel page could not be reached: {exception.Message}");
        }

        var now = _clock();
        var videos = ChannelPageParser.ParseVideos(html, now);
        var channelId = ChannelPageParser.ParseChannelId(html) ?? ChannelAddress.ChannelIdFrom(address);

        // A page with no videos is not a channel with no videos. It is a consent wall, a challenge,
        // or a shape this build no longer understands, and saying so is the whole point of the
        // failure channel: a silent empty success looks healthy forever.
        if (videos.Count == 0)
        {
            return FetchOutcome.Failure("The channel page could not be read: no videos were found in it.");
        }

        var title = ChannelPageParser.ParseChannelTitle(html) ?? channelId ?? "YouTube channel";
        var items = videos.Select(ToItem).ToList();

        var canonical = channelId is null ? address : ChannelAddress.ForChannelId(channelId);
        var feed = new ParsedFeed(
            title,
            channelId is null ? "https://www.youtube.com/" : "https://www.youtube.com/channel/" + channelId,
            channelId is null ? null : "https://www.youtube.com/s/desktop/favicon.ico",
            items);

        return FetchOutcome.Success(feed, canonical);
    }

    /// <summary>
    /// One video as an article. The body is the same embed an Atom feed carried, so the reader
    /// plays it through the path it already has; the duration rides along for the Shorts filter.
    /// </summary>
    private static FeedItem ToItem(ChannelVideo video)
    {
        var body =
            "<iframe id=\"ytplayer\" type=\"text/html\" width=\"640\" height=\"360\" src=\"" +
            "https://www.youtube.com/embed/" + video.VideoId +
            "\" frameborder=\"0\" allowfullscreen=\"\"></iframe>";

        return new FeedItem(
            "yt:video:" + video.VideoId,
            video.Title,
            video.Link,
            body,
            video.PublishedAt,
            video.Duration);
    }
}