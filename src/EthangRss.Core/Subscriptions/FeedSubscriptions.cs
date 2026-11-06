using EthangRss.Core.Data;
using EthangRss.Core.Discovery;
using EthangRss.Core.Feeds;
using EthangRss.Core.Polling;
using EthangRss.Core.YouTube;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Subscriptions;

/// <summary>
/// Adds a feed the way a person expects: paste a feed address or a website and the app works out
/// the rest. Nothing is stored until the feed has been fetched and parsed once, so a typo cannot
/// leave a broken row behind.
/// </summary>
public sealed class FeedSubscriptions
{
    private readonly AppDatabase _database;
    private readonly IFeedSource _source;

    public FeedSubscriptions(AppDatabase database, IFeedSource source)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(source);
        _database = database;
        _source = source;
    }

    public async Task<AddFeedResult> AddAsync(string address, CancellationToken cancellationToken)
    {
        // A channel is one address, not a set of candidates to try, and it is stored under the
        // channel id its page reveals so a handle and an id can never become two subscriptions.
        if (ChannelAddress.IsChannelPage(address))
        {
            return await AddChannelAsync(address, cancellationToken).ConfigureAwait(false);
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return AddFeedResult.Invalid("Enter a full web address, for example https://example.com/feed.xml");
        }

        foreach (var candidate in await CandidatesAsync(uri, cancellationToken).ConfigureAwait(false))
        {
            var outcome = await _source.FetchAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (!outcome.IsSuccess)
            {
                continue;
            }

            var feed = outcome.Feed!;
            var id = Insert(outcome.CanonicalAddress ?? candidate, feed);
            return id is null
                ? AddFeedResult.Invalid("That feed is already in your list.")
                : AddFeedResult.Added(id.Value, feed.Title, feed.Items.Count);
        }

        return AddFeedResult.Invalid("No feed was found at that address.");
    }

    private async Task<AddFeedResult> AddChannelAsync(string address, CancellationToken cancellationToken)
    {
        var outcome = await _source.FetchAsync(address, cancellationToken).ConfigureAwait(false);
        if (!outcome.IsSuccess)
        {
            return AddFeedResult.Invalid(outcome.Error ?? "That channel could not be read.");
        }

        var feed = outcome.Feed!;
        var stored = outcome.CanonicalAddress ?? address;
        var id = Insert(stored, feed);

        return id is null
            ? AddFeedResult.Invalid("That channel is already in your list.")
            : AddFeedResult.Added(id.Value, feed.Title, feed.Items.Count);
    }

    private async Task<IReadOnlyList<string>> CandidatesAsync(Uri uri, CancellationToken cancellationToken)
    {
        var candidates = new List<string> { uri.ToString() };

        if (FeedDiscovery.LooksLikeFeed(uri.ToString()))
        {
            return candidates;
        }

        var page = await _source.FetchPageAsync(uri.ToString(), cancellationToken).ConfigureAwait(false);
        if (page is not null)
        {
            candidates.AddRange(FeedDiscovery.FindFeedUrls(page, uri));
        }

        candidates.AddRange(FeedDiscovery.FallbackUrls(uri));
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private long? Insert(string xmlAddress, ParsedFeed feed)
    {
        long id;
        using (var connection = _database.Open())
        {
            // last_insert_rowid() keeps the previous id when a conflict short-circuits the insert,
            // so the number of rows written is what tells us whether anything was actually added.
            using var command = connection.CreateCommand();
            command.CommandText =
                "insert into feeds (XmlAddress, Title, Website, IconUrl) " +
                "values ($address, $title, $website, $icon) on conflict (XmlAddress) do nothing";
            command.Parameters.AddWithValue("$address", xmlAddress);
            command.Parameters.AddWithValue("$title", feed.Title);
            command.Parameters.AddWithValue("$website", feed.Website);
            command.Parameters.AddWithValue("$icon", (object?)feed.IconUrl ?? DBNull.Value);

            if (command.ExecuteNonQuery() == 0)
            {
                return null;
            }

            using var lastId = connection.CreateCommand();
            lastId.CommandText = "select last_insert_rowid()";
            id = (long)lastId.ExecuteScalar()!;
        }

        if (id == 0)
        {
            return null;
        }

        var repository = new FeedRepository(_database);
        repository.UpsertArticles(id, feed.Items);
        repository.RecordFetchSuccess(id, feed, DateTimeOffset.UtcNow);
        return id;
    }

    public bool Remove(long feedId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "delete from feeds where Id = $id";
        command.Parameters.AddWithValue("$id", feedId);
        return command.ExecuteNonQuery() == 1;
    }

    public void SetInterval(long feedId, int? minutes)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "update feeds set FetchIntervalMinutes = $interval where Id = $id";
        command.Parameters.AddWithValue("$interval", (object?)minutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", feedId);
        command.ExecuteNonQuery();
    }
}

public sealed record AddFeedResult(bool Success, long FeedId, string Message)
{
    public static AddFeedResult Added(long feedId, string title, int articleCount) =>
        new(true, feedId, "Added " + title + " with " + articleCount + " articles.");

    public static AddFeedResult Invalid(string message) => new(false, 0, message);
}
