using EthangRss.Core.YouTube;

namespace EthangRss.Core.Tests.YouTube;

public sealed class ChannelAddressTests
{
    [Theory]
    [InlineData("https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg")]
    [InlineData("https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg/videos")]
    [InlineData("https://youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg")]
    [InlineData("http://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg")]
    [InlineData("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg")]
    public void A_channel_id_is_read_from_every_address_form(string address)
    {
        Assert.Equal("UC6Mjg5R5QOJYjrio1JmP8Fg", ChannelAddress.ChannelIdFrom(address));
    }

    [Theory]
    [InlineData("https://www.youtube.com/@oompaville")]
    [InlineData("https://www.youtube.com/@oompaville/videos")]
    [InlineData("https://youtube.com/@oompaville")]
    public void A_handle_is_recognized_as_a_channel_page_without_a_known_id(string address)
    {
        Assert.True(ChannelAddress.IsChannelPage(address));
        Assert.Null(ChannelAddress.ChannelIdFrom(address));
    }

    [Theory]
    [InlineData("https://example.com/feed.xml")]
    [InlineData("https://www.youtube.com/watch?v=7Cn_0SKci80")]
    [InlineData("https://www.youtube.com/playlist?list=PL123")]
    [InlineData("")]
    [InlineData("not a url")]
    public void Anything_that_is_not_a_channel_is_rejected(string address)
    {
        Assert.False(ChannelAddress.IsChannelPage(address));
        Assert.Null(ChannelAddress.ChannelIdFrom(address));
    }

    [Fact]
    public void The_stored_address_form_is_the_app_s_own_scheme()
    {
        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", ChannelAddress.ForChannelId("UC6Mjg5R5QOJYjrio1JmP8Fg"));
        Assert.True(ChannelAddress.IsChannelAddress("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg"));
        Assert.False(ChannelAddress.IsChannelAddress("https://example.com/feed.xml"));
    }

    [Fact]
    public void The_dead_atom_feed_address_yields_its_channel_id()
    {
        Assert.Equal(
            "UC6Mjg5R5QOJYjrio1JmP8Fg",
            ChannelAddress.ChannelIdFrom("https://www.youtube.com/feeds/videos.xml?channel_id=UC6Mjg5R5QOJYjrio1JmP8Fg"));
    }
}