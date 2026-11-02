using EthangRss.Core.Retention;

namespace EthangRss.Core.Tests.Retention;

public sealed class ArticleRetentionTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly long _feed;

    public ArticleRetentionTests() => _feed = _temp.InsertFeed();

    public void Dispose() => _temp.Dispose();

    private void Seed(string guid, DateTimeOffset published, bool starred = false)
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into articles (FeedId, Guid, Title, Link, Content, PublishedAt, FetchedAt, IsStarred) " +
            "values ($feed, $guid, $title, $link, 'body', $published, $fetched, $starred)";
        command.Parameters.AddWithValue("$feed", _feed);
        command.Parameters.AddWithValue("$guid", guid);
        command.Parameters.AddWithValue("$title", guid);
        command.Parameters.AddWithValue("$link", "https://e.com/" + guid);
        command.Parameters.AddWithValue("$published", published.ToString("O"));
        command.Parameters.AddWithValue("$fetched", published.ToString("O"));
        command.Parameters.AddWithValue("$starred", starred ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private long CountArticles()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void Deletes_only_articles_older_than_the_window()
    {
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        Seed("old", now.AddDays(-100));
        Seed("new", now.AddDays(-10));

        var deleted = ArticleRetention.Apply(_temp.Database, 90, now);

        Assert.Equal(1, deleted);
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Starred_articles_survive_cleanup()
    {
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        Seed("old-star", now.AddDays(-400), starred: true);
        Seed("old", now.AddDays(-400));

        Assert.Equal(1, ArticleRetention.Apply(_temp.Database, 30, now));
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Nothing_is_deleted_when_everything_is_inside_the_window()
    {
        var now = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        Seed("a", now.AddDays(-1));
        Seed("b", now.AddDays(-2));

        Assert.Equal(0, ArticleRetention.Apply(_temp.Database, 30, now));
    }
}
