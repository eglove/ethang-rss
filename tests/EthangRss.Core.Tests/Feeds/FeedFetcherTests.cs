using System.Net;
using EthangRss.Core.Feeds;

namespace EthangRss.Core.Tests.Feeds;

/// <summary>
/// YouTube answers its feed endpoint with an intermittent 404 or 500 that clears on a second
/// request. These tests pin the retry that turns that into a successful fetch, and the rule that
/// keeps a real outage from being hammered.
/// </summary>
public sealed class FeedFetcherTests
{
    private const string FeedXml =
        "<?xml version=\"1.0\"?><rss version=\"2.0\"><channel><title>Retried</title>" +
        "<link>https://example.com</link><item><title>One</title><link>https://example.com/1</link>" +
        "<guid>g1</guid></item></channel></rss>";

    private static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK) { Content = new StringContent(FeedXml) };

    [Fact]
    public async Task A_flaky_404_is_retried_and_the_feed_still_loads()
    {
        var attempts = 0;
        var handler = new StubHttpHandler(_ => Interlocked.Increment(ref attempts) == 1
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : Ok());
        var fetcher = new FeedFetcher(new HttpClient(handler));

        var outcome = await fetcher.FetchAsync("https://example.com/feed", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, handler.CallCount);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public async Task A_flaky_500_is_retried_too()
    {
        var attempts = 0;
        var handler = new StubHttpHandler(_ => Interlocked.Increment(ref attempts) == 1
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Ok());
        var fetcher = new FeedFetcher(new HttpClient(handler));

        var outcome = await fetcher.FetchAsync("https://example.com/feed", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task A_feed_that_keeps_failing_reports_the_last_error_after_bounded_attempts()
    {
        var handler = new StubHttpHandler(HttpStatusCode.NotFound, "gone");
        var fetcher = new FeedFetcher(new HttpClient(handler));

        var outcome = await fetcher.FetchAsync("https://example.com/feed", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal(FeedFetcher.MaxAttempts, handler.CallCount);
    }

    [Fact]
    public async Task A_healthy_feed_is_fetched_once()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, FeedXml);
        var fetcher = new FeedFetcher(new HttpClient(handler));

        var outcome = await fetcher.FetchAsync("https://example.com/feed", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task An_unparseable_body_is_not_retried()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "this is not xml at all");
        var fetcher = new FeedFetcher(new HttpClient(handler));

        var outcome = await fetcher.FetchAsync("https://example.com/feed", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task A_cancelled_fetch_is_not_retried()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new StubHttpHandler(HttpStatusCode.NotFound, "gone");
        var fetcher = new FeedFetcher(new HttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fetcher.FetchAsync("https://example.com/feed", cancelled.Token));
    }
}