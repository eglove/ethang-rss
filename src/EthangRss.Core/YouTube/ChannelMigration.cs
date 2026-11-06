using EthangRss.Core.Data;

namespace EthangRss.Core.YouTube;

/// <summary>
/// Rewrites YouTube subscriptions that point at the retired Atom endpoint to the channel form
/// this build fetches. The row keeps its id, so every article already stored stays attached to
/// the feed it came from.
/// </summary>
public static class ChannelMigration
{
    /// <summary>Rewrites every eligible feed and returns how many rows changed.</summary>
    public static int Run(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var pending = new List<(long Id, string Address)>();
        using (var connection = database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "select Id, XmlAddress from feeds";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var address = reader.GetString(1);
                if (ChannelAddress.IsChannelAddress(address))
                {
                    continue;
                }

                if (ChannelAddress.ChannelIdFrom(address) is { } channelId)
                {
                    pending.Add((id, ChannelAddress.ForChannelId(channelId)));
                }
            }
        }

        if (pending.Count == 0)
        {
            return 0;
        }

        var changed = 0;
        using var write = database.Open();
        using var transaction = write.BeginTransaction();
        foreach (var (id, address) in pending)
        {
            using var update = write.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                "update feeds set XmlAddress = $address, LastError = null, LastErrorAt = null " +
                "where Id = $id and XmlAddress not like 'youtube://%'";
            update.Parameters.AddWithValue("$address", address);
            update.Parameters.AddWithValue("$id", id);
            changed += update.ExecuteNonQuery();
        }

        transaction.Commit();
        return changed;
    }
}