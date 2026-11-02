using EthangRss.Core.Discovery;

namespace EthangRss.Core.Tests.Discovery;

public sealed class FeedDiscoveryTests
{
    [Fact]
    public void Finds_the_declared_alternate_feed_link_first()
    {
        const string HTML = "<html><head>" +
            "<link rel='stylesheet' href='/style.css'>" +
            "<link rel='alternate' type='application/rss+xml' href='/feed.xml'>" +
            "<link rel='alternate' type='application/atom+xml' href='/atom.xml'>" +
            "</head></html>";

        var found = FeedDiscovery.FindFeedUrls(HTML, new Uri("https://example.com/blog"));

        Assert.Equal(new[] { "https://example.com/feed.xml", "https://example.com/atom.xml" }, found);
    }

    [Fact]
    public void Falls_back_to_the_common_feed_paths_when_nothing_is_declared()
    {
        var found = FeedDiscovery.FallbackUrls(new Uri("https://example.com/blog/"));

        Assert.Contains("https://example.com/blog/feed", found);
        Assert.Contains("https://example.com/blog/rss", found);
        Assert.Contains("https://example.com/blog/rss.xml", found);
        Assert.Contains("https://example.com/blog/atom.xml", found);
        Assert.Contains("https://example.com/blog/feed.xml", found);
    }

    [Fact]
    public void A_page_with_no_feed_links_yields_nothing()
    {
        var found = FeedDiscovery.FindFeedUrls("<html><head><title>x</title></head></html>", new Uri("https://example.com"));

        Assert.Empty(found);
    }

    [Fact]
    public void Declared_links_are_made_absolute()
    {
        const string HTML = "<link rel='alternate' type='application/rss+xml' href='feed.xml'>" +
            "<link rel='alternate' type='application/rss+xml' href='/feed.xml'>";

        var found = FeedDiscovery.FindFeedUrls(HTML, new Uri("https://example.com/dir/page"));

        // A relative href resolves against the page, an absolute one against the site root, so the
        // two declarations are genuinely different addresses and both are kept.
        Assert.Equal(2, found.Count);
        Assert.Contains("https://example.com/dir/feed.xml", found);
        Assert.Contains("https://example.com/feed.xml", found);
    }

    [Fact]
    public void A_url_that_is_already_a_feed_is_used_directly()
    {
        Assert.True(FeedDiscovery.LooksLikeFeed("https://example.com/feed.xml"));
        Assert.True(FeedDiscovery.LooksLikeFeed("https://example.com/rss"));
        Assert.True(FeedDiscovery.LooksLikeFeed("https://example.com/atom.xml"));
        Assert.True(FeedDiscovery.LooksLikeFeed("https://news.google.com/rss/search?q=rust"));
        Assert.False(FeedDiscovery.LooksLikeFeed("https://example.com/blog/post"));
    }
}
