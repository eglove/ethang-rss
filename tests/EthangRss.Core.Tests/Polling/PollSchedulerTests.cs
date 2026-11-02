using System.Collections.Concurrent;
using EthangRss.Core.Data;
using EthangRss.Core.Feeds;
using EthangRss.Core.Polling;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Tests.Polling;

public sealed class PollSchedulerTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeFetcher : IFeedSource
    {
        public ConcurrentBag<string> Requested { get; } = new();

        public Dictionary<string, Exception> Throws { get; } = new(StringComparer.Ordinal);

        public int InFlight;

        public int MaxInFlight;

        public async Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref InFlight);
            InterlockedMax(ref MaxInFlight, current);
            try
            {
                Requested.Add(xmlAddress);
                await Task.Delay(10, cancellationToken);
                if (Throws.TryGetValue(xmlAddress, out var exception))
                {
                    throw exception;
                }

                return FetchOutcome.Success(new ParsedFeed("Fetched", xmlAddress, null, Array.Empty<FeedItem>()));
            }
            finally
            {
                Interlocked.Decrement(ref InFlight);
            }
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int previous;
            do
            {
                previous = target;
                if (previous >= value)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, previous) != previous);
        }
    }

    private long InsertFeed(string address, string? lastFetchedAt, int? interval = null)
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into feeds (XmlAddress, Title, LastFetchedAt, FetchIntervalMinutes) " +
            "values ($address, $title, $fetched, $interval); select last_insert_rowid();";
        command.Parameters.AddWithValue("$address", address);
        command.Parameters.AddWithValue("$title", address);
        command.Parameters.AddWithValue("$fetched", (object?)lastFetchedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("$interval", (object?)interval ?? DBNull.Value);
        return (long)command.ExecuteScalar()!;
    }

    private long CountArticles()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        return (long)command.ExecuteScalar()!;
    }

    private PollScheduler NewScheduler(FakeFetcher fetcher, int intervalMinutes = 30)
    {
        return new PollScheduler(
            _temp.Database,
            fetcher,
            new FeedRepository(_temp.Database),
            new PollOptions { IntervalMinutes = intervalMinutes },
            () => Now);
    }

    [Fact]
    public async Task Feed_never_fetched_is_due()
    {
        InsertFeed("https://a.example.com/feed", null);
        var fetcher = new FakeFetcher();

        var summary = await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, summary.Polled);
        Assert.Single(fetcher.Requested);
    }

    [Fact]
    public async Task Feed_fetched_recently_is_skipped()
    {
        InsertFeed("https://a.example.com/feed", Now.AddMinutes(-5).ToString("O"));
        var fetcher = new FakeFetcher();

        var summary = await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, summary.Polled);
        Assert.Empty(fetcher.Requested);
    }

    [Fact]
    public async Task Feed_fetched_longer_ago_than_the_interval_is_due()
    {
        InsertFeed("https://a.example.com/feed", Now.AddMinutes(-31).ToString("O"));
        var fetcher = new FakeFetcher();

        var summary = await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, summary.Polled);
    }

    [Fact]
    public async Task Per_feed_interval_override_makes_an_old_feed_due()
    {
        InsertFeed("https://a.example.com/feed", Now.AddMinutes(-10).ToString("O"), interval: 5);
        var fetcher = new FakeFetcher();

        var summary = await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, summary.Polled);
    }

    [Fact]
    public async Task Per_feed_interval_override_holds_a_feed_back()
    {
        InsertFeed("https://a.example.com/feed", Now.AddMinutes(-10).ToString("O"), interval: 120);
        var fetcher = new FakeFetcher();

        var summary = await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, summary.Polled);
    }

    [Fact]
    public async Task One_failing_feed_does_not_stop_the_others()
    {
        var failing = InsertFeed("https://bad.example.com/feed", null);
        InsertFeed("https://good.example.com/feed", null);
        var fetcher = new FakeFetcher();
        fetcher.Throws["https://bad.example.com/feed"] = new HttpRequestException("network down");

        var summary = await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, summary.Polled);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Succeeded);

        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select LastError from feeds where Id = $id";
        command.Parameters.AddWithValue("$id", failing);
        Assert.Contains("network down", (string)command.ExecuteScalar()!);
    }

    [Fact]
    public async Task Concurrency_never_exceeds_the_configured_limit()
    {
        for (var index = 0; index < 20; index++)
        {
            InsertFeed($"https://feed{index}.example.com/feed", null);
        }

        var fetcher = new FakeFetcher();

        await NewScheduler(fetcher).RunOnceAsync(CancellationToken.None);

        Assert.Equal(20, fetcher.Requested.Count);
        Assert.True(fetcher.MaxInFlight <= 6, $"max in flight was {fetcher.MaxInFlight}");
    }

    [Fact]
    public async Task New_articles_are_counted_and_stored()
    {
        var feedId = InsertFeed("https://a.example.com/feed", null);

        var fetcher = new FakeFetcher();
        var scheduler = new PollScheduler(
            _temp.Database,
            new ItemFetcher(),
            new FeedRepository(_temp.Database),
            new PollOptions(),
            () => Now);

        var summary = await scheduler.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, summary.NewArticles);
        Assert.Equal(2, CountArticles());
        Assert.Equal(1, summary.Succeeded);
    }

    private sealed class ItemFetcher : IFeedSource
    {
        public Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken)
        {
            return Task.FromResult(FetchOutcome.Success(new ParsedFeed(
                "Fetched",
                xmlAddress,
                null,
                [
                    new FeedItem("g1", "One", "https://e.com/1", "body", Now),
                    new FeedItem("g2", "Two", "https://e.com/2", "body", Now),
                ])));
        }
    }
}
