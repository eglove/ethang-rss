using EthangRss.Core.Data;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Feeds;

/// <summary>Writes fetch results back into the reader database.</summary>
public sealed class FeedRepository
{
    private readonly AppDatabase _database;

    public FeedRepository(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <summary>
    /// Inserts the articles that are not yet stored and returns how many were new.
    /// Dedupe is the (FeedId, Guid) unique constraint, so a re-fetch of the same window is free.
    /// </summary>
    /// <summary>Set false to keep YouTube Shorts. Default matches the app's default setting.</summary>
    public bool FilterYoutubeShorts { get; set; } = true;

    /// <summary>
    /// The longest clip still counted as a Short. YouTube's ceiling has moved from one minute to
    /// three, so this is a setting rather than a constant.
    /// </summary>
    public int ShortsMaxSeconds { get; set; } = 180;

    public int UpsertArticles(long feedId, IReadOnlyList<FeedItem> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "insert into articles (FeedId, Guid, Title, Link, Content, PublishedAt, FetchedAt, DurationSeconds) " +
            "values ($feed, $guid, $title, $link, $content, $published, $fetched, $duration) " +
            "on conflict (FeedId, Guid) do nothing";

        var feedParameter = command.Parameters.Add("$feed", SqliteType.Integer);
        var guidParameter = command.Parameters.Add("$guid", SqliteType.Text);
        var titleParameter = command.Parameters.Add("$title", SqliteType.Text);
        var linkParameter = command.Parameters.Add("$link", SqliteType.Text);
        var contentParameter = command.Parameters.Add("$content", SqliteType.Text);
        var publishedParameter = command.Parameters.Add("$published", SqliteType.Text);
        var fetchedParameter = command.Parameters.Add("$fetched", SqliteType.Text);
        var durationParameter = command.Parameters.Add("$duration", SqliteType.Integer);

        var inserted = 0;
        var fetchedAt = DateTimeOffset.UtcNow.ToString("O");

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Guid) || IsFiltered(item))
            {
                continue;
            }

            feedParameter.Value = feedId;
            guidParameter.Value = item.Guid;
            titleParameter.Value = item.Title;
            linkParameter.Value = item.Link;
            contentParameter.Value = item.Content;
            publishedParameter.Value = item.PublishedAt?.ToString("O") ?? (object)DBNull.Value;
            fetchedParameter.Value = fetchedAt;
            durationParameter.Value = item.Duration is { } duration
                ? (long)duration.TotalSeconds
                : DBNull.Value;

            inserted += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return inserted;
    }

    /// <summary>
    /// A clip is dropped by its length when the source gave one, because Shorts are served on
    /// ordinary watch links and the address alone does not identify them. Only an item with no
    /// duration falls back to the link rule.
    /// </summary>
    private bool IsFiltered(FeedItem item)
    {
        if (!FilterYoutubeShorts)
        {
            return false;
        }

        if (item.Duration is { } duration)
        {
            return duration.TotalSeconds <= ShortsMaxSeconds;
        }

        return ShortsFilter.IsShorts(item.Link);
    }

    public void RecordFetchSuccess(long feedId, ParsedFeed feed, DateTimeOffset fetchedAt)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "update feeds set Title = $title, Website = $website, IconUrl = $icon, " +
            "LastFetchedAt = $fetched, LastError = null, LastErrorAt = null where Id = $id";
        command.Parameters.AddWithValue("$title", feed.Title);
        command.Parameters.AddWithValue("$website", feed.Website);
        command.Parameters.AddWithValue("$icon", (object?)feed.IconUrl ?? DBNull.Value);
        command.Parameters.AddWithValue("$fetched", fetchedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", feedId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Records a failure. LastFetchedAt is deliberately left alone so the feed stays due and
    /// is retried on the next poll rather than being pushed back by a full interval.
    /// </summary>
    public void RecordFetchFailure(long feedId, string error, DateTimeOffset occurredAt)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "update feeds set LastError = $error, LastErrorAt = $at where Id = $id";
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$at", occurredAt.ToString("O"));
        command.Parameters.AddWithValue("$id", feedId);
        command.ExecuteNonQuery();
    }
}
