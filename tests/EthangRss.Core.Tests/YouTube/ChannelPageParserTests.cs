using EthangRss.Core.YouTube;

namespace EthangRss.Core.Tests.YouTube;

/// <summary>
/// The fixture is a real channel page: the envelope and the three entries inside it were
/// lifted verbatim from a live /videos page, so these tests fail when the shape YouTube
/// actually serves changes, not when a hand-written sample drifts.
/// </summary>
public sealed class ChannelPageParserTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "YouTube", "Fixtures", "channel-videos.html"));

    [Fact]
    public void Every_video_on_the_page_is_extracted()
    {
        var videos = ChannelPageParser.ParseVideos(Fixture());

        Assert.Equal(3, videos.Count);
    }

    [Fact]
    public void A_video_carries_its_id_title_and_duration()
    {
        var first = ChannelPageParser.ParseVideos(Fixture())[0];

        Assert.Equal("7Cn_0SKci80", first.VideoId);
        Assert.Equal("They're Calling Him Epstein 2.0...", first.Title);
        Assert.Equal(TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(40), first.Duration);
        Assert.Equal("https://www.youtube.com/watch?v=7Cn_0SKci80", first.Link);
    }

    [Fact]
    public void A_relative_age_becomes_an_approximate_publish_time()
    {
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var videos = ChannelPageParser.ParseVideos(Fixture(), now);

        // The page says "14h ago" and "5d ago" for the first two entries.
        Assert.Equal(now.AddHours(-14), videos[0].PublishedAt);
        Assert.Equal(now.AddDays(-5), videos[1].PublishedAt);
    }

    [Theory]
    [InlineData("14h ago", 14, 0, 0)]
    [InlineData("5d ago", 0, 5, 0)]
    [InlineData("3w ago", 0, 21, 0)]
    [InlineData("2mo ago", 0, 60, 0)]
    [InlineData("1y ago", 0, 365, 0)]
    [InlineData("just now", 0, 0, 0)]
    public void Relative_ages_are_understood(string text, int hours, int days, int minutes)
    {
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        var parsed = ChannelPageParser.ParseRelativeTime(text, now);

        Assert.Equal(now.AddHours(-hours).AddDays(-days).AddMinutes(-minutes), parsed);
    }

    [Fact]
    public void An_unreadable_age_is_null_rather_than_wrong()
    {
        var now = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        Assert.Null(ChannelPageParser.ParseRelativeTime("streamed live", now));
    }

    [Fact]
    public void A_page_with_no_initial_data_yields_nothing_instead_of_throwing()
    {
        Assert.Empty(ChannelPageParser.ParseVideos("<html><body>consent wall</body></html>"));
    }

    [Fact]
    public void The_channel_title_is_read_from_the_page()
    {
        Assert.Equal("oompaville", ChannelPageParser.ParseChannelTitle(Fixture()));
    }

    [Fact]
    public void The_channel_id_is_read_from_the_page()
    {
        Assert.Equal("UC6Mjg5R5QOJYjrio1JmP8Fg", ChannelPageParser.ParseChannelId(Fixture()));
    }
}