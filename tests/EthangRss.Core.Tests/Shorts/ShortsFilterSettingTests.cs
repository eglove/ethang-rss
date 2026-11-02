using EthangRss.Core.Feeds;

namespace EthangRss.Core.Tests.Shorts;

public sealed class ShortsFilterSettingTests
{
    private const string WITH_SHORT =
        "<rss version='2.0'><channel><title>T</title><link>https://e.com/</link>" +
        "<item><title>Real</title><link>https://e.com/a</link></item>" +
        "<item><title>Clip</title><link>https://www.youtube.com/shorts/xyz</link></item>" +
        "</channel></rss>";

    [Fact]
    public void Filtering_on_drops_shorts()
    {
        var feed = FeedParser.Parse(WITH_SHORT, new Uri("https://e.com/feed.xml"), filterShorts: true).Feed!;

        var single = Assert.Single(feed.Items);
        Assert.Equal("Real", single.Title);
    }

    [Fact]
    public void Filtering_off_keeps_shorts()
    {
        var feed = FeedParser.Parse(WITH_SHORT, new Uri("https://e.com/feed.xml"), filterShorts: false).Feed!;

        Assert.Equal(2, feed.Items.Count);
        Assert.Contains(feed.Items, item => item.Link.Contains("shorts", StringComparison.Ordinal));
    }

    [Fact]
    public void The_default_overload_keeps_the_historical_behaviour()
    {
        var feed = FeedParser.Parse(WITH_SHORT, new Uri("https://e.com/feed.xml")).Feed!;

        Assert.Single(feed.Items);
    }
}
