using EthangRss.Core.Subscriptions;

namespace EthangRss.Core.Tests.Subscriptions;

/// <summary>
/// Right-clicking a feed opens the page a person would visit, which is not always the address
/// the app stores: a channel is kept under the app's own scheme, and a feed document is a
/// machine's address rather than the site it belongs to.
/// </summary>
public sealed class FeedPageAddressTests
{
    [Fact]
    public void A_channel_opens_its_page_on_youtube_rather_than_the_app_s_own_address()
    {
        Assert.Equal(
            "https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg",
            FeedPageAddress.For("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", "https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg"));
    }

    [Fact]
    public void A_channel_with_no_stored_website_still_opens_its_page()
    {
        Assert.Equal(
            "https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg",
            FeedPageAddress.For("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", null));
    }

    [Fact]
    public void A_feed_opens_the_site_it_belongs_to()
    {
        Assert.Equal(
            "https://example.com",
            FeedPageAddress.For("https://example.com/feed.xml", "https://example.com"));
    }

    [Fact]
    public void A_feed_with_no_website_opens_its_own_address()
    {
        Assert.Equal(
            "https://example.com/feed.xml",
            FeedPageAddress.For("https://example.com/feed.xml", null));
    }

    [Fact]
    public void A_website_that_is_not_a_web_page_is_ignored_in_favour_of_the_feed_address()
    {
        Assert.Equal(
            "https://example.com/feed.xml",
            FeedPageAddress.For("https://example.com/feed.xml", "javascript:alert(1)"));
    }

    [Fact]
    public void An_address_nothing_can_be_opened_from_is_reported_as_no_page()
    {
        Assert.Null(FeedPageAddress.For("youtube://channel/not-an-id", null));
        Assert.Null(FeedPageAddress.For("", null));
        Assert.Null(FeedPageAddress.For("not a url", null));
    }
}