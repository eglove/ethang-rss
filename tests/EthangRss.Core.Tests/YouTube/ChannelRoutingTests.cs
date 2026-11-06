using System.Net;
using EthangRss.Core.Feeds;
using EthangRss.Core.Polling;
using EthangRss.Core.YouTube;

namespace EthangRss.Core.Tests.YouTube;

/// <summary>
/// The reader has one fetch seam. These tests hold the router to it: a channel address must
/// reach the channel fetcher and an ordinary feed address must reach the feed fetcher, without
/// either caller knowing which is which.
/// </summary>
public sealed class ChannelRoutingTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "YouTube", "Fixtures", "channel-videos.html"));

    private static FeedSourceRouter Router(StubHttpHandler handler) =>
        new(new FeedFetcher(new HttpClient(handler)), new ChannelFetcher(new HttpClient(handler)));

    [Fact]
    public async Task A_channel_address_is_served_by_the_channel_fetcher()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, Fixture());

        var outcome = await Router(handler).FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Contains("/channel/UC6Mjg5R5QOJYjrio1JmP8Fg/videos", handler.LastRequestUri);
    }

    [Fact]
    public async Task An_ordinary_feed_address_is_served_by_the_feed_fetcher()
    {
        const string rss = "<rss version=\"2.0\"><channel><title>Example</title>" +
            "<item><title>A</title><link>https://e.com/a</link><guid>g1</guid></item></channel></rss>";
        var handler = new StubHttpHandler(HttpStatusCode.OK, rss);

        var outcome = await Router(handler).FetchAsync("https://e.com/feed.xml", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("https://e.com/feed.xml", handler.LastRequestUri);
    }

    [Fact]
    public void The_router_recognizes_a_channel_address_without_touching_the_network()
    {
        Assert.True(FeedSourceRouter.IsChannel("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg"));
        Assert.True(FeedSourceRouter.IsChannel("https://www.youtube.com/@oompaville"));
        Assert.False(FeedSourceRouter.IsChannel("https://e.com/feed.xml"));
    }
}