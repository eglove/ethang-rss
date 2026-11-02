using EthangRss.Core.Data;

namespace EthangRss.Core.Import;

/// <summary>
/// Writes the subscribed feeds from a Cloudflare dump into the local database. Subscriptions only:
/// no articles and no read state come across, because the reader refetches everything itself.
/// Idempotent by XML address, so running it twice adds nothing.
/// </summary>
public sealed class SubscriptionImporter
{
    private readonly AppDatabase _database;

    public SubscriptionImporter(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public ImportResult Import(string dumpPath)
    {
        var dump = new D1DumpReader().Read(dumpPath);
        var imported = 0;
        var alreadyPresent = 0;

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var feed in dump.SubscribedFeeds)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "insert into feeds (XmlAddress, Title, Website, IconUrl) " +
                "values ($address, $title, $website, $icon) on conflict (XmlAddress) do nothing";
            command.Parameters.AddWithValue("$address", feed.XmlAddress);
            command.Parameters.AddWithValue("$title", string.IsNullOrWhiteSpace(feed.Title) ? feed.XmlAddress : feed.Title);
            command.Parameters.AddWithValue("$website", (object?)feed.Website ?? DBNull.Value);
            command.Parameters.AddWithValue("$icon", (object?)feed.IconUrl ?? DBNull.Value);

            if (command.ExecuteNonQuery() == 1)
            {
                imported++;
            }
            else
            {
                alreadyPresent++;
            }
        }

        transaction.Commit();
        return new ImportResult(imported, alreadyPresent, dump.SkippedUnsubscribed);
    }
}
