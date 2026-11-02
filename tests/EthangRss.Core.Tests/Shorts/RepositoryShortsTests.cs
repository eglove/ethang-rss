using EthangRss.Core.Feeds;

namespace EthangRss.Core.Tests.Shorts;

public sealed class RepositoryShortsTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private static IReadOnlyList<FeedItem> Items() =>
    [
        new FeedItem("g1", "Real", "https://e.com/a", "body", DateTimeOffset.UtcNow),
        new FeedItem("g2", "Clip", "https://youtube.com/shorts/xyz", "body", DateTimeOffset.UtcNow),
    ];

    private int Count()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        return (int)(long)command.ExecuteScalar()!;
    }

    [Fact]
    public void With_filtering_on_only_real_articles_are_stored()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = true };

        Assert.Equal(1, repository.UpsertArticles(feed, Items()));
        Assert.Equal(1, Count());
    }

    [Fact]
    public void With_filtering_off_shorts_are_stored_too()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = false };

        Assert.Equal(2, repository.UpsertArticles(feed, Items()));
        Assert.Equal(2, Count());
    }
}
