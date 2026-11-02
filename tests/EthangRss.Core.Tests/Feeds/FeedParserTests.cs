using EthangRss.Core.Feeds;

namespace EthangRss.Core.Tests.Feeds;

public sealed class FeedParserTests
{
    private static string Fixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Feeds", "Fixtures", name);
        return File.ReadAllText(path);
    }


    [Fact]
    public void Parses_rss_channel_title_website_and_items()
    {
        var result = FeedParser.Parse(Fixture("rss2.xml"), new Uri("https://example.com/feed.xml"));

        Assert.True(result.IsSuccess);
        var feed = result.Feed!;
        Assert.Equal("Example Feed", feed.Title);
        Assert.Equal("https://example.com/", feed.Website);
        Assert.Equal(2, feed.Items.Count);
    }

    [Fact]
    public void Rss_item_carries_guid_link_title_and_body()
    {
        var feed = FeedParser.Parse(Fixture("rss2.xml"), new Uri("https://example.com/feed.xml")).Feed!;

        var first = feed.Items[0];
        Assert.Equal("guid-first", first.Guid);
        Assert.Equal("https://example.com/first", first.Link);
        Assert.Equal("First post", first.Title);
        Assert.Equal("The first body", first.Content);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), first.PublishedAt);
    }

    [Fact]
    public void Item_without_a_guid_falls_back_to_its_link()
    {
        var feed = FeedParser.Parse(Fixture("rss2.xml"), new Uri("https://example.com/feed.xml")).Feed!;

        var second = feed.Items[1];
        Assert.Equal("https://example.com/second", second.Guid);
    }

    [Fact]
    public void Youtube_shorts_links_are_filtered_out()
    {
        var feed = FeedParser.Parse(Fixture("rss2.xml"), new Uri("https://example.com/feed.xml")).Feed!;

        Assert.DoesNotContain(feed.Items, item => item.Link.Contains("youtube.com/shorts", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_atom_entries_preferring_the_alternate_link()
    {
        var result = FeedParser.Parse(Fixture("atom.xml"), new Uri("https://atom.example.org/feed.xml"));

        Assert.True(result.IsSuccess);
        var feed = result.Feed!;
        Assert.Equal("Atom Example", feed.Title);
        Assert.Equal("https://atom.example.org/", feed.Website);
        Assert.Equal("https://atom.example.org/icon.png", feed.IconUrl);

        var entry = Assert.Single(feed.Items);
        Assert.Equal("urn:uuid:1", entry.Guid);
        Assert.Equal("https://atom.example.org/entry-1-view", entry.Link);
        Assert.Equal("Summary text", entry.Content);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero), entry.PublishedAt);
    }

    [Fact]
    public void Non_rfc822_ut_token_is_normalized_to_gmt()
    {
        var xml = "<rss version='2.0'><channel><title>T</title><link>https://e.com/</link>" +
                  "<item><title>I</title><link>https://e.com/i</link><pubDate>Tue, 03 Nov 2026 10:00:00 UT</pubDate></item>" +
                  "</channel></rss>";

        var feed = FeedParser.Parse(xml, new Uri("https://e.com/feed.xml")).Feed!;

        Assert.Equal(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero), feed.Items[0].PublishedAt);
    }

    [Fact]
    public void Missing_date_falls_back_to_a_recent_timestamp()
    {
        var xml = "<rss version='2.0'><channel><title>T</title><link>https://e.com/</link>" +
                  "<item><title>I</title><link>https://e.com/i</link></item></channel></rss>";

        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var feed = FeedParser.Parse(xml, new Uri("https://e.com/feed.xml")).Feed!;

        Assert.NotNull(feed.Items[0].PublishedAt);
        Assert.InRange(feed.Items[0].PublishedAt!.Value, before, DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void Item_without_a_title_gets_the_placeholder()
    {
        var xml = "<rss version='2.0'><channel><title>T</title><link>https://e.com/</link>" +
                  "<item><link>https://e.com/i</link></item></channel></rss>";

        var feed = FeedParser.Parse(xml, new Uri("https://e.com/feed.xml")).Feed!;

        Assert.Equal("No Title", feed.Items[0].Title);
    }

    [Fact]
    public void Malformed_xml_returns_a_failure_instead_of_throwing()
    {
        var result = FeedParser.Parse(Fixture("malformed.xml"), new Uri("https://example.com/feed.xml"));

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Empty_feed_parses_with_zero_items()
    {
        var result = FeedParser.Parse(Fixture("empty.xml"), new Uri("https://empty.example.com/feed.xml"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Feed!.Items);
    }

    [Fact]
    public void Icon_falls_back_to_the_host_favicon_when_absent()
    {
        var feed = FeedParser.Parse(Fixture("rss2.xml"), new Uri("https://example.com/feed.xml")).Feed!;

        Assert.Equal("https://example.com/favicon.ico", feed.IconUrl);
    }

    [Fact]
    public void Media_description_inside_a_media_group_becomes_the_body()
    {
        var feed = FeedParser.Parse(Fixture("youtube.xml"), new Uri("https://www.youtube.com/feeds/videos.xml?channel_id=UC123")).Feed!;

        var entry = Assert.Single(feed.Items);
        Assert.Contains("The video description text.", entry.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Media_thumbnail_is_kept_as_an_image_in_the_body()
    {
        var feed = FeedParser.Parse(Fixture("youtube.xml"), new Uri("https://www.youtube.com/feeds/videos.xml?channel_id=UC123")).Feed!;

        var entry = Assert.Single(feed.Items);
        Assert.Contains("https://i2.ytimg.com/vi/abc/hqdefault.jpg", entry.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void A_feed_with_no_body_leaves_content_empty_rather_than_inventing_one()
    {
        var feed = FeedParser.Parse(Fixture("headlines.xml"), new Uri("https://headlines.example.com/feed.xml")).Feed!;

        var item = Assert.Single(feed.Items);
        Assert.Equal(string.Empty, item.Content);
    }
}