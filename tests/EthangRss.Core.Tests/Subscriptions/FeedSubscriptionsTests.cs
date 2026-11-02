using EthangRss.Core.Feeds;
using EthangRss.Core.Polling;
using EthangRss.Core.Subscriptions;

namespace EthangRss.Core.Tests.Subscriptions;

public sealed class FeedSubscriptionsTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private sealed class ScriptedSource : IFeedSource
    {
        public Dictionary<string, ParsedFeed> Feeds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Pages { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Requested { get; } = [];

        public Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken)
        {
            Requested.Add(xmlAddress);
            return Task.FromResult(Feeds.TryGetValue(xmlAddress, out var feed)
                ? FetchOutcome.Success(feed)
                : FetchOutcome.Failure("not found", 404));
        }

        public Task<string?> FetchPageAsync(string address, CancellationToken cancellationToken) =>
            Task.FromResult(Pages.TryGetValue(address, out var page) ? page : null);
    }

    private static ParsedFeed SimpleFeed(string title) => new(
        title,
        "https://example.com/",
        null,
        [new FeedItem("g1", "An article", "https://example.com/1", "body", DateTimeOffset.UtcNow)]);

    [Fact]
    public async Task Adding_a_feed_address_stores_it_with_its_first_articles()
    {
        var source = new ScriptedSource();
        source.Feeds["https://example.com/feed.xml"] = SimpleFeed("Example");
        var subscriptions = new FeedSubscriptions(_temp.Database, source);

        var result = await subscriptions.AddAsync("https://example.com/feed.xml", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Example", result.Message);
        Assert.Single(new Core.Queries.ReaderQueries(_temp.Database).GetFeeds());
    }

    [Fact]
    public async Task Adding_a_website_discovers_the_feed_it_advertises()
    {
        var source = new ScriptedSource();
        source.Pages["https://blog.example.com"] =
            "<link rel='alternate' type='application/rss+xml' href='/feed.xml'>";
        source.Feeds["https://blog.example.com/feed.xml"] = SimpleFeed("Discovered");
        var subscriptions = new FeedSubscriptions(_temp.Database, source);

        var result = await subscriptions.AddAsync("https://blog.example.com", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("Discovered", result.Message);
    }

    [Fact]
    public async Task A_website_with_no_feed_falls_back_to_conventional_paths()
    {
        var source = new ScriptedSource();
        source.Pages["https://bare.example.com"] = "<html>no feed declared</html>";
        source.Feeds["https://bare.example.com/feed"] = SimpleFeed("Bare");
        var subscriptions = new FeedSubscriptions(_temp.Database, source);

        var result = await subscriptions.AddAsync("https://bare.example.com", CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task An_address_with_no_feed_stores_nothing()
    {
        var subscriptions = new FeedSubscriptions(_temp.Database, new ScriptedSource());

        var result = await subscriptions.AddAsync("https://empty.example.com", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(new Core.Queries.ReaderQueries(_temp.Database).GetFeeds());
    }

    [Fact]
    public async Task A_nonsense_address_is_rejected_without_a_request()
    {
        var source = new ScriptedSource();
        var subscriptions = new FeedSubscriptions(_temp.Database, source);

        var result = await subscriptions.AddAsync("not a url", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(source.Requested);
    }

    [Fact]
    public async Task Adding_the_same_feed_twice_reports_it_already_exists()
    {
        var source = new ScriptedSource();
        source.Feeds["https://example.com/feed.xml"] = SimpleFeed("Example");
        var subscriptions = new FeedSubscriptions(_temp.Database, source);

        await subscriptions.AddAsync("https://example.com/feed.xml", CancellationToken.None);
        var second = await subscriptions.AddAsync("https://example.com/feed.xml", CancellationToken.None);

        Assert.False(second.Success);
        Assert.Single(new Core.Queries.ReaderQueries(_temp.Database).GetFeeds());
    }

    [Fact]
    public void Removing_a_feed_deletes_it()
    {
        var feedId = _temp.InsertFeed();
        var subscriptions = new FeedSubscriptions(_temp.Database, new ScriptedSource());

        Assert.True(subscriptions.Remove(feedId));
        Assert.Empty(new Core.Queries.ReaderQueries(_temp.Database).GetFeeds());
    }

    [Fact]
    public void An_interval_override_is_stored_and_can_be_cleared()
    {
        var feedId = _temp.InsertFeed();
        var subscriptions = new FeedSubscriptions(_temp.Database, new ScriptedSource());

        subscriptions.SetInterval(feedId, 5);
        Assert.Equal(5, new Core.Queries.ReaderQueries(_temp.Database).GetFeeds().Single().FetchIntervalMinutes);

        subscriptions.SetInterval(feedId, null);
        Assert.Null(new Core.Queries.ReaderQueries(_temp.Database).GetFeeds().Single().FetchIntervalMinutes);
    }
}
