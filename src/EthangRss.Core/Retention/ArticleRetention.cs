using EthangRss.Core.Data;

namespace EthangRss.Core.Retention;

/// <summary>
/// Optional cleanup of old articles. Off by default: a local disk is cheap, and deleting history is
/// the one action that cannot be undone. Starred articles are never removed, and unread and read
/// articles age out on their own windows.
/// </summary>
public static class ArticleRetention
{
    /// <summary>Applies both windows of a policy, returning how many articles were removed.</summary>
    public static int Apply(AppDatabase database, RetentionPolicy policy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(policy);

        var deleted = 0;

        if (policy.UnreadDays is { } unreadDays)
        {
            deleted += Apply(database, unreadDays, now, isRead: false);
        }

        if (policy.ReadDays is { } readDays)
        {
            deleted += Apply(database, readDays, now, isRead: true);
        }

        return deleted;
    }

    /// <summary>Applies one window to one read state. Starred articles are always spared.</summary>
    public static int Apply(AppDatabase database, int retentionDays, DateTimeOffset now, bool isRead)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);

        var cutoff = now.AddDays(-retentionDays).ToString("O");

        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "delete from articles where IsStarred = 0 and IsRead = $isRead " +
            "and PublishedAt is not null and PublishedAt < $cutoff";
        command.Parameters.AddWithValue("$isRead", isRead ? 1 : 0);
        command.Parameters.AddWithValue("$cutoff", cutoff);
        return command.ExecuteNonQuery();
    }
}
