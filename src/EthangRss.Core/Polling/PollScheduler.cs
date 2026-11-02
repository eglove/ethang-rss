using EthangRss.Core.Data;
using EthangRss.Core.Feeds;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Polling;

/// <summary>
/// Decides which feeds are due and fetches them with bounded concurrency. A feed that fails is
/// recorded and skipped; it never stops the pass or the feeds behind it.
/// </summary>
public sealed class PollScheduler
{
    private readonly AppDatabase _database;
    private readonly IFeedSource _source;
    private readonly FeedRepository _repository;
    private readonly PollOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<bool> _filterShorts;

    public PollScheduler(
        AppDatabase database,
        IFeedSource source,
        FeedRepository repository,
        PollOptions options,
        Func<DateTimeOffset> clock,
        Func<bool>? filterShorts = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _database = database;
        _source = source;
        _repository = repository;
        _options = options;
        _clock = clock;
        _filterShorts = filterShorts ?? (() => true);
    }

    /// <summary>True while a pass is running, so the UI can disable refresh.</summary>
    public bool IsPolling { get; private set; }

    /// <summary>Runs one pass over every feed that is due.</summary>
    public Task<PollSummary> RunOnceAsync(CancellationToken cancellationToken) =>
        RunAsync(ignoreInterval: false, cancellationToken);

    /// <summary>Runs one pass over every feed regardless of when it was last fetched.</summary>
    public Task<PollSummary> RefreshNowAsync(CancellationToken cancellationToken) =>
        RunAsync(ignoreInterval: true, cancellationToken);

    private async Task<PollSummary> RunAsync(bool ignoreInterval, CancellationToken cancellationToken)
    {
        if (IsPolling)
        {
            return PollSummary.Empty;
        }

        IsPolling = true;
        try
        {
            var now = _clock();
            var due = GetDueFeeds(now, ignoreInterval);
            if (due.Count == 0)
            {
                return PollSummary.Empty;
            }

            var polled = 0;
            var succeeded = 0;
            var failed = 0;
            var newArticles = 0;
            var gate = new SemaphoreSlim(_options.MaxConcurrency);
            var mutex = new object();

            var work = due.Select(async feed =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var outcome = await FetchSafely(feed.XmlAddress, cancellationToken).ConfigureAwait(false);
                    var inserted = 0;
                    var error = outcome.Error;

                    if (outcome.IsSuccess)
                    {
                        _repository.FilterYoutubeShorts = _filterShorts();
                        inserted = _repository.UpsertArticles(feed.Id, outcome.Feed!.Items);
                        _repository.RecordFetchSuccess(feed.Id, outcome.Feed, _clock());
                    }
                    else
                    {
                        _repository.RecordFetchFailure(feed.Id, error ?? "Unknown error", _clock());
                    }

                    lock (mutex)
                    {
                        polled++;
                        newArticles += inserted;
                        if (outcome.IsSuccess)
                        {
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                }
                finally
                {
                    gate.Release();
                }
            });

            await Task.WhenAll(work).ConfigureAwait(false);

            return new PollSummary
            {
                Polled = polled,
                Succeeded = succeeded,
                Failed = failed,
                NewArticles = newArticles,
            };
        }
        finally
        {
            IsPolling = false;
        }
    }

    private async Task<FetchOutcome> FetchSafely(string xmlAddress, CancellationToken cancellationToken)
    {
        try
        {
            return await _source.FetchAsync(xmlAddress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return FetchOutcome.Failure(exception.Message);
        }
    }

    private List<DueFeed> GetDueFeeds(DateTimeOffset now, bool ignoreInterval)
    {
        var feeds = new List<DueFeed>();

        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "select Id, XmlAddress, LastFetchedAt, FetchIntervalMinutes from feeds order by Id";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var feed = new DueFeed(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3));

            if (ignoreInterval || IsDue(feed, now))
            {
                feeds.Add(feed);
            }
        }

        return feeds;
    }

    private bool IsDue(DueFeed feed, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(feed.LastFetchedAt))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(feed.LastFetchedAt, out var lastFetched))
        {
            return true;
        }

        var interval = TimeSpan.FromMinutes(feed.FetchIntervalMinutes ?? _options.IntervalMinutes);
        return now - lastFetched >= interval;
    }
}
