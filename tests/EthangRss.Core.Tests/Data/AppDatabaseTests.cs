using EthangRss.Core.Data;
using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Tests.Data;

public sealed class AppDatabaseTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ethangrss-test-{Guid.NewGuid():N}.db");

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

    private AppDatabase NewDatabase()
    {
        var db = new AppDatabase(_path);
        db.Migrate();
        return db;
    }

    [Fact]
    public void Migrate_creates_every_table_and_the_full_text_index()
    {
        var db = NewDatabase();

        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select name from sqlite_master where type = 'table' order by name";
        var names = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        Assert.Contains("feeds", names);
        Assert.Contains("articles", names);
        Assert.Contains("settings", names);
        Assert.Contains("articles_fts", names);
    }

    [Fact]
    public void Migrate_twice_is_idempotent()
    {
        var db = NewDatabase();

        db.Migrate();

        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = 'articles'";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public void Open_enables_write_ahead_logging_and_foreign_keys()
    {
        var db = NewDatabase();

        using var connection = db.Open();

        using var journal = connection.CreateCommand();
        journal.CommandText = "pragma journal_mode";
        Assert.Equal("wal", (journal.ExecuteScalar() as string)?.ToLowerInvariant());

        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "pragma foreign_keys";
        Assert.Equal(1L, foreignKeys.ExecuteScalar());
    }

    [Fact]
    public void Deleting_a_feed_cascades_to_its_articles()
    {
        var db = NewDatabase();

        using var connection = db.Open();
        using var insertFeed = connection.CreateCommand();
        insertFeed.CommandText = "insert into feeds (XmlAddress, Title) values ('https://example.com/feed.xml', 'Example'); select last_insert_rowid();";
        var feedId = (long)insertFeed.ExecuteScalar()!;

        using var insertArticle = connection.CreateCommand();
        insertArticle.CommandText = "insert into articles (FeedId, Guid, Title, Link, FetchedAt) values ($feed, 'g1', 'An article', 'https://example.com/1', '2026-01-01T00:00:00Z')";
        insertArticle.Parameters.AddWithValue("$feed", feedId);
        insertArticle.ExecuteNonQuery();

        using var deleteFeed = connection.CreateCommand();
        deleteFeed.CommandText = "delete from feeds where Id = $id";
        deleteFeed.Parameters.AddWithValue("$id", feedId);
        deleteFeed.ExecuteNonQuery();

        using var count = connection.CreateCommand();
        count.CommandText = "select count(*) from articles";
        Assert.Equal(0L, count.ExecuteScalar());
    }

    [Fact]
    public void Article_text_is_searchable_through_the_full_text_index()
    {
        var db = NewDatabase();

        using var connection = db.Open();
        using var insertFeed = connection.CreateCommand();
        insertFeed.CommandText = "insert into feeds (XmlAddress, Title) values ('https://example.com/feed.xml', 'Example'); select last_insert_rowid();";
        var feedId = (long)insertFeed.ExecuteScalar()!;

        using var insertArticle = connection.CreateCommand();
        insertArticle.CommandText = "insert into articles (FeedId, Guid, Title, Link, Content, FetchedAt) values ($feed, 'g1', 'Rust ownership', 'https://example.com/1', 'The borrow checker rejects this program', '2026-01-01T00:00:00Z')";
        insertArticle.Parameters.AddWithValue("$feed", feedId);
        insertArticle.ExecuteNonQuery();

        using var search = connection.CreateCommand();
        search.CommandText = "select count(*) from articles_fts where articles_fts match 'borrow'";
        Assert.Equal(1L, search.ExecuteScalar());

        using var miss = connection.CreateCommand();
        miss.CommandText = "select count(*) from articles_fts where articles_fts match 'kotlin'";
        Assert.Equal(0L, miss.ExecuteScalar());
    }

    [Fact]
    public void Default_path_lives_under_local_app_data()
    {
        Assert.Contains(Path.Combine("EthangRss", "ethang-rss.db"), new AppDatabase().DatabasePath);
    }
}
