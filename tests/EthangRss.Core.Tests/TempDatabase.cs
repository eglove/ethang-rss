using EthangRss.Core.Data;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Tests;

/// <summary>A throwaway database on disk, removed when the test ends.</summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ethangrss-test-{Guid.NewGuid():N}.db");

    public TempDatabase()
    {
        Database = new AppDatabase(_path);
        Database.Migrate();
    }

    public AppDatabase Database { get; }

    public long InsertFeed(string xmlAddress = "https://example.com/feed.xml", string title = "Example")
    {
        using var connection = Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "insert into feeds (XmlAddress, Title) values ($address, $title); select last_insert_rowid();";
        command.Parameters.AddWithValue("$address", xmlAddress);
        command.Parameters.AddWithValue("$title", title);
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }
    }
}
