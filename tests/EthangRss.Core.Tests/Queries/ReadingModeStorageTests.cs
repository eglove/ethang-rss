using EthangRss.Core.Data;
using EthangRss.Core.Feeds;
using EthangRss.Core.Queries;

namespace EthangRss.Core.Tests.Queries;

public sealed class ReadingModeStorageTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private long StoreArticle(string content)
    {
        var feedId = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database);
        repository.UpsertArticles(feedId, [
            new FeedItem("g1", "An article", "https://example.com/one", content,
                new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero))]);

        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select Id from articles where Guid = 'g1'";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void A_new_article_has_no_recovered_text_yet()
    {
        var id = StoreArticle("<p>Read the rest on the site.</p>");

        var detail = new ReaderQueries(_temp.Database).GetArticle(id);

        Assert.NotNull(detail);
        Assert.Null(detail.FullContent);
    }

    [Fact]
    public void Recovered_text_is_stored_and_read_back()
    {
        var id = StoreArticle("<p>Read the rest on the site.</p>");
        var queries = new ReaderQueries(_temp.Database);

        queries.SaveFullContent(id, "<p>The whole article.</p>", new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero));

        var detail = queries.GetArticle(id);
        Assert.NotNull(detail);
        Assert.Equal("<p>The whole article.</p>", detail.FullContent);
    }

    [Fact]
    public void A_page_that_offered_nothing_is_recorded_as_tried_rather_than_left_unknown()
    {
        var id = StoreArticle("<p>Read the rest on the site.</p>");
        var queries = new ReaderQueries(_temp.Database);

        queries.SaveFullContent(id, null, new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero));

        var detail = queries.GetArticle(id);
        Assert.NotNull(detail);
        Assert.Equal(string.Empty, detail.FullContent);
        Assert.True(detail.FullContentAttempted);
    }

    [Fact]
    public void A_teaser_body_is_never_replaced_by_the_recovered_text()
    {
        var id = StoreArticle("<p>Read the rest on the site.</p>");
        var queries = new ReaderQueries(_temp.Database);

        queries.SaveFullContent(id, "<p>The whole article.</p>", new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero));

        // The feed's own body stays as it arrived: the recovered text is a second field, so a
        // re-fetch of the feed cannot overwrite what reading mode found.
        var detail = queries.GetArticle(id);
        Assert.Equal("<p>Read the rest on the site.</p>", detail!.Content);
    }
}