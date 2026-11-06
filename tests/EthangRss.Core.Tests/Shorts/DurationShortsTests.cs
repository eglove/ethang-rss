using EthangRss.Core.Feeds;

namespace EthangRss.Core.Tests.Shorts;

/// <summary>
/// A Short is a clip by length, not by address: YouTube serves Shorts on ordinary watch links,
/// so the link-based filter misses them. Duration is the fact that settles it.
/// </summary>
public sealed class DurationShortsTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private static FeedItem Video(string guid, int seconds, string link = "https://www.youtube.com/watch?v=aaaaaaaaaaa") =>
        new(guid, "t", link, "body", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(seconds));

    private int Count()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        return (int)(long)command.ExecuteScalar()!;
    }

    [Fact]
    public void A_clip_is_dropped_by_its_length_even_on_a_watch_link()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = true, ShortsMaxSeconds = 180 };

        Assert.Equal(0, repository.UpsertArticles(feed, [Video("g1", 45)]));
    }

    [Fact]
    public void A_full_length_video_is_kept()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = true, ShortsMaxSeconds = 180 };

        Assert.Equal(1, repository.UpsertArticles(feed, [Video("g1", 2740)]));
    }

    [Fact]
    public void The_threshold_is_inclusive_at_three_minutes()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = true, ShortsMaxSeconds = 180 };

        Assert.Equal(1, repository.UpsertArticles(feed, [Video("g1", 181)]));
        Assert.Equal(1, Count());
    }

    [Fact]
    public void An_item_with_no_duration_falls_back_to_the_link_rule()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = true };

        var undated = new FeedItem("g1", "t", "https://youtube.com/shorts/xyz", "body", DateTimeOffset.UtcNow);
        Assert.Equal(0, repository.UpsertArticles(feed, [undated]));
    }

    [Fact]
    public void Turning_the_filter_off_keeps_clips()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = false };

        Assert.Equal(1, repository.UpsertArticles(feed, [Video("g1", 45)]));
    }

    [Fact]
    public void The_duration_is_stored_so_the_list_can_show_it()
    {
        var feed = _temp.InsertFeed();
        var repository = new FeedRepository(_temp.Database) { FilterYoutubeShorts = false };

        repository.UpsertArticles(feed, [Video("g1", 2740)]);

        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select DurationSeconds from articles where Guid = 'g1'";
        Assert.Equal(2740L, (long)command.ExecuteScalar()!);
    }
}