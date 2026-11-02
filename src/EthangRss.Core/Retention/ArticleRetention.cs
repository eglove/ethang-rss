using EthangRss.Core.Data;

namespace EthangRss.Core.Retention;

/// <summary>
/// Optional cleanup of old articles. Off by default: a local disk is cheap, and deleting history is
/// the one action that cannot be undone. Starred articles are never removed.
/// </summary>
public static class ArticleRetention
{
    public static int Apply(AppDatabase database, int retentionDays, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retentionDays, 1);

        var cutoff = now.AddDays(-retentionDays).ToString("O");

        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "delete from articles where IsStarred = 0 and PublishedAt is not null and PublishedAt < $cutoff";
        command.Parameters.AddWithValue("$cutoff", cutoff);
        return command.ExecuteNonQuery();
    }
}
