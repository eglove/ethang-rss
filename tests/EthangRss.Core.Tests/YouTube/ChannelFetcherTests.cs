using System.Net;
using EthangRss.Core.Feeds;
using EthangRss.Core.YouTube;

namespace EthangRss.Core.Tests.YouTube;

public sealed class ChannelFetcherTests
{
    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "YouTube", "Fixtures", "channel-videos.html"));

    private static ChannelFetcher Fetcher(HttpStatusCode status, string body, out StubHttpHandler handler)
    {
        handler = new StubHttpHandler(status, body);
        return new ChannelFetcher(new HttpClient(handler));
    }

    [Fact]
    public async Task A_channel_address_yields_a_feed_of_its_videos()
    {
        var fetcher = Fetcher(HttpStatusCode.OK, Fixture(), out _);

        var outcome = await fetcher.FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("oompaville", outcome.Feed!.Title);
        Assert.Equal(3, outcome.Feed.Items.Count);
    }

    [Fact]
    public async Task A_video_becomes_an_article_that_plays_in_the_reader()
    {
        var fetcher = Fetcher(HttpStatusCode.OK, Fixture(), out _);

        var outcome = await fetcher.FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);
        var first = outcome.Feed!.Items[0];

        Assert.Equal("yt:video:7Cn_0SKci80", first.Guid);
        Assert.Equal("https://www.youtube.com/watch?v=7Cn_0SKci80", first.Link);
        Assert.Contains("youtube.com/embed/7Cn_0SKci80", first.Content);
        Assert.Equal(TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(40), first.Duration);
    }

    [Fact]
    public async Task A_handle_address_is_resolved_to_the_channel_it_names()
    {
        var fetcher = Fetcher(HttpStatusCode.OK, Fixture(), out var handler);

        var outcome = await fetcher.FetchAsync("https://www.youtube.com/@oompaville", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", outcome.CanonicalAddress);
        Assert.Contains("/@oompaville/videos", handler.LastRequestUri);
    }

    [Fact]
    public async Task A_channel_id_address_canonicalizes_to_itself()
    {
        var fetcher = Fetcher(HttpStatusCode.OK, Fixture(), out var handler);

        var outcome = await fetcher.FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", outcome.CanonicalAddress);
        Assert.Contains("/channel/UC6Mjg5R5QOJYjrio1JmP8Fg/videos", handler.LastRequestUri);
    }

    [Fact]
    public async Task A_channel_that_answers_with_a_consent_wall_is_a_visible_failure()
    {
        var fetcher = Fetcher(HttpStatusCode.OK, "<html><body>before you continue</body></html>", out _);

        var outcome = await fetcher.FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("could not be read", outcome.Error);
    }

    [Fact]
    public async Task A_404_is_reported_with_its_status()
    {
        var fetcher = Fetcher(HttpStatusCode.NotFound, "nope", out _);

        var outcome = await fetcher.FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
    }

    [Fact]
    public async Task Shorts_are_dropped_when_the_setting_is_on()
    {
        var page = Fixture().Replace("45:40", "0:42");
        var fetcher = Fetcher(HttpStatusCode.OK, page, out _) ;

        var outcome = await fetcher.FetchAsync("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        // The fetcher keeps everything; dropping is the repository's job with the duration in hand.
        Assert.Equal(3, outcome.Feed!.Items.Count);
        Assert.Equal(TimeSpan.FromSeconds(42), outcome.Feed.Items[0].Duration);
    }
}