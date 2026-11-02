using System.Text;
using EthangRss.Core.Data;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Queries;

/// <summary>
/// Every read the UI performs and every write it triggers. Queries are synchronous on purpose:
/// the readers are small and the UI already moves work off the dispatcher.
/// </summary>
public sealed class ReaderQueries
{
    private readonly AppDatabase _database;

    public ReaderQueries(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public IReadOnlyList<FeedSummary> GetFeeds()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "select f.Id, f.Title, f.XmlAddress, f.Website, f.IconUrl, f.LastError, f.LastFetchedAt, " +
            "f.FetchIntervalMinutes, " +
            "(select count(*) from articles a where a.FeedId = f.Id and a.IsRead = 0) as Unread, " +
            "(select count(*) from articles a where a.FeedId = f.Id) as Total " +
            "from feeds f order by f.Title collate nocase";

        var feeds = new List<FeedSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            feeds.Add(new FeedSummary(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7)));
        }

        return feeds;
    }

    public FolderCounts GetFolderCounts()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "select (select count(*) from articles where IsRead = 0), " +
            "(select count(*) from articles where IsStarred = 1), " +
            "(select count(*) from articles)";

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new FolderCounts(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : new FolderCounts(0, 0, 0);
    }

    public IReadOnlyList<ArticleListItem> GetArticles(
        ArticleScope scope,
        long? feedId,
        string? search,
        int offset,
        int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var searching = !string.IsNullOrWhiteSpace(search);
        var sql = new StringBuilder();
        sql.Append("select a.Id, a.Guid, a.Title, a.Link, a.FeedId, a.IsRead, a.IsStarred, a.PublishedAt, ");
        sql.Append("substr(coalesce(a.Content, ''), 1, 400), f.Title, f.IconUrl from articles a ");
        sql.Append("join feeds f on f.Id = a.FeedId ");

        if (searching)
        {
            sql.Append("join articles_fts on articles_fts.rowid = a.Id ");
        }

        sql.Append("where 1 = 1 ");

        using var connection = _database.Open();
        using var command = connection.CreateCommand();

        switch (scope)
        {
            case ArticleScope.Unread:
                sql.Append("and a.IsRead = 0 ");
                break;
            case ArticleScope.Starred:
                sql.Append("and a.IsStarred = 1 ");
                break;
            default:
                break;
        }

        if (feedId is { } id)
        {
            sql.Append("and a.FeedId = $feed ");
            command.Parameters.AddWithValue("$feed", id);
        }

        if (searching)
        {
            sql.Append("and articles_fts match $search ");
            command.Parameters.AddWithValue("$search", ToFtsQuery(search!));
        }

        sql.Append("order by a.PublishedAt desc, a.Id desc limit $limit offset $offset");
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$offset", offset);
        command.CommandText = sql.ToString();

        var items = new List<ArticleListItem>();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new ArticleListItem(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.GetInt64(4),
                    reader.GetInt32(5) == 1,
                    reader.GetInt32(6) == 1,
                    ParseDate(reader.IsDBNull(7) ? null : reader.GetString(7)),
                    reader.IsDBNull(8) ? null : Truncate(reader.GetString(8), 200)));
            }
        }
        catch (SqliteException)
        {
            // A malformed FTS expression is a user typing, not a crash: show no results.
            return Array.Empty<ArticleListItem>();
        }

        return items;
    }

    public ArticleDetail? GetArticle(long articleId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "select a.Id, a.Title, a.Link, coalesce(a.Content, ''), f.Title, a.FeedId, a.IsRead, " +
            "a.IsStarred, a.PublishedAt from articles a join feeds f on f.Id = a.FeedId where a.Id = $id";
        command.Parameters.AddWithValue("$id", articleId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ArticleDetail(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetInt32(6) == 1,
            reader.GetInt32(7) == 1,
            ParseDate(reader.IsDBNull(8) ? null : reader.GetString(8)));
    }

    public int MarkRead(long articleId, bool isRead) => ExecuteCount(
        "update articles set IsRead = $value where Id = $id",
        ("$value", isRead ? 1 : 0),
        ("$id", articleId));

    /// <summary>Marks every unread article read, optionally limited to one feed. Returns the count.</summary>
    public int MarkAllRead(long? feedId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = feedId is null
            ? "update articles set IsRead = 1 where IsRead = 0"
            : "update articles set IsRead = 1 where IsRead = 0 and FeedId = $feed";

        if (feedId is { } id)
        {
            command.Parameters.AddWithValue("$feed", id);
        }

        return command.ExecuteNonQuery();
    }

    /// <summary>Flips the star and reports its new state.</summary>
    public bool ToggleStar(long articleId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "update articles set IsStarred = case IsStarred when 1 then 0 else 1 end where Id = $id; " +
            "select IsStarred from articles where Id = $id";
        command.Parameters.AddWithValue("$id", articleId);
        return Convert.ToInt64(command.ExecuteScalar()) == 1;
    }

    public int DeleteFeed(long feedId) => ExecuteCount(
        "delete from feeds where Id = $id",
        ("$id", feedId));

    private int ExecuteCount(string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Turns arbitrary typed text into an FTS expression that will not throw: every term is
    /// quoted, which neutralizes operators like AND, OR, and NEAR instead of interpreting them.
    /// </summary>
    internal static string ToFtsQuery(string search)
    {
        var terms = search
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.Replace("\"", string.Empty, StringComparison.Ordinal))
            .Where(term => term.Length > 0)
            .Select(term => $"\"{term}\" *".Replace(" *", "*", StringComparison.Ordinal))
            .ToList();

        return terms.Count == 0 ? "\"\"" : string.Join(" AND ", terms);
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
