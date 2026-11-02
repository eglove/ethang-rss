using System.Net;
using EthangRss.Core.Data;
using EthangRss.Core.Feeds;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Tests.Data;

public sealed class FeedRepositoryTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly Uri Address = new("https://example.com/feed.xml");

    private static ParsedFeed SampleFeed(params (string Guid, string Title, string Link)[] items)
    {
        var parsed = items.Select(item => new FeedItem(
            item.Guid, item.Title, item.Link, "body", new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero))).ToList();

        return new ParsedFeed("Example Feed", "https://example.com/", "https://example.com/icon.png", parsed);
    }

    private long CountArticles()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void Successful_fetch_stores_every_article()
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);

        var inserted = repository.UpsertArticles(feedId, SampleFeed(
            ("g1", "One", "https://example.com/1"),
            ("g2", "Two", "https://example.com/2")).Items);

        Assert.Equal(2, inserted);
        Assert.Equal(2, CountArticles());
    }

    [Fact]
    public void Re_fetching_the_same_articles_inserts_nothing_new()
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);
        var feed = SampleFeed(("g1", "One", "https://example.com/1"));

        repository.UpsertArticles(feedId, feed.Items);
        var second = repository.UpsertArticles(feedId, feed.Items);

        Assert.Equal(0, second);
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Articles_with_a_blank_guid_are_skipped()
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);
        var feed = SampleFeed(("", "One", "https://example.com/1"));

        Assert.Equal(0, repository.UpsertArticles(feedId, feed.Items));
    }

    [Fact]
    public void Shorts_links_are_never_stored()
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);
        var feed = SampleFeed(("g1", "Short", "https://youtube.com/shorts/xyz"));

        Assert.Equal(0, repository.UpsertArticles(feedId, feed.Items));
    }

    [Fact]
    public void Success_updates_metadata_and_clears_a_previous_error()
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);

        repository.RecordFetchFailure(feedId, "boom", DateTimeOffset.UtcNow);
        repository.RecordFetchSuccess(feedId, SampleFeed(), DateTimeOffset.UtcNow);

        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select Title, Website, IconUrl, LastError, LastFetchedAt from feeds where Id = $id";
        command.Parameters.AddWithValue("$id", feedId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal("Example Feed", reader.GetString(0));
        Assert.Equal("https://example.com/", reader.GetString(1));
        Assert.Equal("https://example.com/icon.png", reader.GetString(2));
        Assert.True(reader.IsDBNull(3));
        Assert.False(reader.IsDBNull(4));
    }

    [Fact]
    public void Failure_records_the_error_without_moving_last_fetched_at()
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);
        var fetched = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        repository.RecordFetchSuccess(feedId, SampleFeed(), fetched);
        repository.RecordFetchFailure(feedId, "the server said no", fetched.AddHours(1));

        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select LastError, LastFetchedAt from feeds where Id = $id";
        command.Parameters.AddWithValue("$id", feedId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal("the server said no", reader.GetString(0));
        Assert.StartsWith("2026-11-01", reader.GetString(1));
    }

    [Fact]
    public async Task Fetcher_reports_a_transport_failure_instead_of_throwing()
    {
        using var handler = new StubHttpHandler(HttpStatusCode.InternalServerError, "nope");
        using var client = new HttpClient(handler);
        var fetcher = new FeedFetcher(client);

        var outcome = await fetcher.FetchAsync("https://example.com/feed.xml", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("500", outcome.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fetcher_reports_malformed_xml_as_a_failure()
    {
        using var handler = new StubHttpHandler(HttpStatusCode.OK, "<rss><channel>oops");
        using var client = new HttpClient(handler);
        var fetcher = new FeedFetcher(client);

        var outcome = await fetcher.FetchAsync("https://example.com/feed.xml", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
    }

    [Fact]
    public async Task Fetcher_returns_parsed_items_on_success()
    {
        var xml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Feeds", "Fixtures", "rss2.xml"));
        using var handler = new StubHttpHandler(HttpStatusCode.OK, xml);
        using var client = new HttpClient(handler);
        var fetcher = new FeedFetcher(client);

        var outcome = await fetcher.FetchAsync("https://example.com/feed.xml", CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(2, outcome.Feed!.Items.Count);
    }
}
