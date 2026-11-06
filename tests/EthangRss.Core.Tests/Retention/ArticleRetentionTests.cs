using EthangRss.Core.Retention;

namespace EthangRss.Core.Tests.Retention;

public sealed class ArticleRetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase _temp = new();
    private readonly long _feed;

    public ArticleRetentionTests() => _feed = _temp.InsertFeed();

    public void Dispose() => _temp.Dispose();

    private void Seed(string guid, DateTimeOffset published, bool starred = false, bool isRead = false)
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into articles (FeedId, Guid, Title, Link, Content, PublishedAt, FetchedAt, IsRead, IsStarred) " +
            "values ($feed, $guid, $title, $link, 'body', $published, $fetched, $read, $starred)";
        command.Parameters.AddWithValue("$feed", _feed);
        command.Parameters.AddWithValue("$guid", guid);
        command.Parameters.AddWithValue("$title", guid);
        command.Parameters.AddWithValue("$link", "https://e.com/" + guid);
        command.Parameters.AddWithValue("$published", published.ToString("O"));
        command.Parameters.AddWithValue("$fetched", published.ToString("O"));
        command.Parameters.AddWithValue("$read", isRead ? 1 : 0);
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
        Seed("old", Now.AddDays(-100));
        Seed("new", Now.AddDays(-10));

        var deleted = ArticleRetention.Apply(_temp.Database, 90, Now, isRead: false);

        Assert.Equal(1, deleted);
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Starred_articles_survive_cleanup()
    {
        Seed("old-star", Now.AddDays(-400), starred: true);
        Seed("old", Now.AddDays(-400));

        Assert.Equal(1, ArticleRetention.Apply(_temp.Database, 30, Now, isRead: false));
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Nothing_is_deleted_when_everything_is_inside_the_window()
    {
        Seed("a", Now.AddDays(-1));
        Seed("b", Now.AddDays(-2));

        Assert.Equal(0, ArticleRetention.Apply(_temp.Database, 30, Now, isRead: false));
    }

    [Fact]
    public void Unread_articles_are_spared_by_the_read_window()
    {
        Seed("unread-old", Now.AddDays(-400));
        Seed("read-old", Now.AddDays(-400), isRead: true);

        var deleted = ArticleRetention.Apply(_temp.Database, new RetentionPolicy(null, 30), Now);

        Assert.Equal(1, deleted);
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Read_articles_are_spared_by_the_unread_window()
    {
        Seed("unread-old", Now.AddDays(-400));
        Seed("read-old", Now.AddDays(-400), isRead: true);

        var deleted = ArticleRetention.Apply(_temp.Database, new RetentionPolicy(30, null), Now);

        Assert.Equal(1, deleted);
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Each_window_uses_its_own_length()
    {
        Seed("read-recent", Now.AddDays(-10), isRead: true);
        Seed("read-old", Now.AddDays(-60), isRead: true);
        Seed("unread-recent", Now.AddDays(-60));
        Seed("unread-old", Now.AddDays(-200));

        // Read goes at 30 days, unread at 180, so the same age means different things per state.
        var deleted = ArticleRetention.Apply(_temp.Database, new RetentionPolicy(180, 30), Now);

        Assert.Equal(2, deleted);
        Assert.Equal(2, CountArticles());
    }

    [Fact]
    public void A_policy_that_keeps_everything_deletes_nothing()
    {
        Seed("old", Now.AddDays(-4000));
        Seed("read-old", Now.AddDays(-4000), isRead: true);

        Assert.Equal(0, ArticleRetention.Apply(_temp.Database, RetentionPolicy.KeepEverything, Now));
        Assert.Equal(2, CountArticles());
    }
}
