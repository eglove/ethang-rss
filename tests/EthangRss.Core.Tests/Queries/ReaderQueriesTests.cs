using EthangRss.Core.Queries;

namespace EthangRss.Core.Tests.Queries;

public sealed class ReaderQueriesTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly ReaderQueries _queries;

    private readonly long _alpha;
    private readonly long _beta;
    private readonly long _gamma;

    public ReaderQueriesTests()
    {
        _queries = new ReaderQueries(_temp.Database);
        _alpha = _temp.InsertFeed("https://alpha.example.com/feed", "Alpha");
        _beta = _temp.InsertFeed("https://beta.example.com/feed", "Beta");
        _gamma = _temp.InsertFeed("https://gamma.example.com/feed", "Gamma");

        var baseTime = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        Seed(_alpha, "a1", "Alpha one", baseTime.AddHours(1));
        Seed(_alpha, "a2", "Alpha two", baseTime.AddHours(2));
        Seed(_beta, "b1", "Beta one", baseTime.AddHours(3));
        Seed(_beta, "b2", "Beta two", baseTime.AddHours(4));
        Seed(_gamma, "g1", "Gamma one", baseTime.AddHours(5));
    }

    public void Dispose() => _temp.Dispose();

    private void Seed(long feedId, string guid, string title, DateTimeOffset published, string content = "body")
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into articles (FeedId, Guid, Title, Link, Content, PublishedAt, FetchedAt) " +
            "values ($feed, $guid, $title, $link, $content, $published, $fetched)";
        command.Parameters.AddWithValue("$feed", feedId);
        command.Parameters.AddWithValue("$guid", guid);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$link", $"https://example.com/{guid}");
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$published", published.ToString("O"));
        command.Parameters.AddWithValue("$fetched", published.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void Execute(string sql)
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Feeds_are_listed_alphabetically_with_unread_counts()
    {
        var feeds = _queries.GetFeeds();

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, feeds.Select(feed => feed.Title));
        Assert.All(feeds, feed => Assert.Equal(feed.ArticleCount, feed.UnreadCount));
    }

    [Fact]
    public void Folder_counts_track_unread_and_starred_totals()
    {
        Execute("update articles set IsRead = 1 where Guid = 'a1'");
        Execute("update articles set IsStarred = 1 where Guid = 'b1'");

        var counts = _queries.GetFolderCounts();

        Assert.Equal(4, counts.Unread);
        Assert.Equal(1, counts.Starred);
        Assert.Equal(5, counts.All);
    }

    [Fact]
    public void Unread_scope_hides_read_articles()
    {
        Execute("update articles set IsRead = 1 where Guid = 'a1'");

        var articles = _queries.GetArticles(ArticleScope.Unread, null, null, 0, 50);

        Assert.Equal(4, articles.Count);
        Assert.DoesNotContain(articles, article => article.Guid == "a1");
    }

    [Fact]
    public void Feed_filter_returns_only_that_feed()
    {
        var articles = _queries.GetArticles(ArticleScope.All, _beta, null, 0, 50);

        Assert.Equal(2, articles.Count);
        Assert.All(articles, article => Assert.Equal("Beta", article.FeedTitle));
    }

    [Fact]
    public void Starred_scope_returns_only_starred()
    {
        Execute("update articles set IsStarred = 1 where Guid = 'g1'");

        var articles = _queries.GetArticles(ArticleScope.Starred, null, null, 0, 50);

        var single = Assert.Single(articles);
        Assert.Equal("g1", single.Guid);
    }

    [Fact]
    public void Articles_are_newest_first()
    {
        var articles = _queries.GetArticles(ArticleScope.All, null, null, 0, 50);

        Assert.Equal("g1", articles[0].Guid);
        Assert.Equal("a1", articles[^1].Guid);
    }

    [Fact]
    public void Paging_skips_and_limits()
    {
        var page = _queries.GetArticles(ArticleScope.All, null, null, 2, 2);

        Assert.Equal(2, page.Count);
        Assert.Equal("b1", page[0].Guid);
        Assert.Equal("a2", page[1].Guid);
    }

    [Fact]
    public void Search_matches_a_body_word()
    {
        Seed(_alpha, "a3", "Unrelated title", DateTimeOffset.UtcNow, "the borrow checker rejects it");

        var hits = _queries.GetArticles(ArticleScope.All, null, "borrow", 0, 50);

        var single = Assert.Single(hits);
        Assert.Equal("a3", single.Guid);
    }

    [Fact]
    public void Search_matches_a_title_word()
    {
        var hits = _queries.GetArticles(ArticleScope.All, null, "Gamma", 0, 50);

        var single = Assert.Single(hits);
        Assert.Equal("g1", single.Guid);
    }

    [Fact]
    public void Search_with_fts_operators_does_not_throw()
    {
        var exception = Record.Exception(() => _queries.GetArticles(ArticleScope.All, null, "a* AND (b OR \"c\") -d", 0, 50));

        Assert.Null(exception);
    }

    [Fact]
    public void Mark_read_updates_the_row()
    {
        var article = _queries.GetArticles(ArticleScope.All, null, null, 0, 1)[0];

        _queries.MarkRead(article.Id, true);
        var counts = _queries.GetFolderCounts();

        Assert.Equal(4, counts.Unread);
    }

    [Fact]
    public void Mark_all_read_in_a_feed_leaves_other_feeds_alone()
    {
        var affected = _queries.MarkAllRead(_alpha);

        Assert.Equal(2, affected);
        Assert.Equal(0, _queries.GetFeeds().Single(feed => feed.Id == _alpha).UnreadCount);
    }

    [Fact]
    public void Toggling_a_star_flips_it_both_ways()
    {
        var article = _queries.GetArticles(ArticleScope.All, null, null, 0, 1)[0];

        Assert.True(_queries.ToggleStar(article.Id));
        Assert.Equal(1, _queries.GetFolderCounts().Starred);

        Assert.False(_queries.ToggleStar(article.Id));
        Assert.Equal(0, _queries.GetFolderCounts().Starred);
    }

    [Fact]
    public void Deleting_a_feed_removes_its_articles_and_search_entries()
    {
        _queries.DeleteFeed(_gamma);

        Assert.Equal(2, _queries.GetFeeds().Count);
        Assert.Empty(_queries.GetArticles(ArticleScope.All, _gamma, null, 0, 50));
        Assert.Empty(_queries.GetArticles(ArticleScope.All, null, "Gamma", 0, 50));
    }

    [Fact]
    public void Detail_returns_the_full_body()
    {
        var article = _queries.GetArticles(ArticleScope.All, null, null, 0, 1)[0];

        var detail = _queries.GetArticle(article.Id);

        Assert.NotNull(detail);
        Assert.Equal("body", detail!.Content);
    }
}
