using EthangRss.Core.Data;
using EthangRss.Core.YouTube;

namespace EthangRss.Core.Tests.YouTube;

/// <summary>
/// Every YouTube subscription in an existing database points at an Atom endpoint that no longer
/// exists. Migration rewrites those rows in place, so the articles already stored stay attached
/// to the feed they came from instead of being orphaned or re-fetched.
/// </summary>
public sealed class ChannelMigrationTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private long InsertDeadFeed(string channelId, string title)
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into feeds (XmlAddress, Title) values ($address, $title); select last_insert_rowid();";
        command.Parameters.AddWithValue("$address", "https://www.youtube.com/feeds/videos.xml?channel_id=" + channelId);
        command.Parameters.AddWithValue("$title", title);
        return (long)command.ExecuteScalar()!;
    }

    private string? AddressOf(long feedId)
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select XmlAddress from feeds where Id = $id";
        command.Parameters.AddWithValue("$id", feedId);
        return command.ExecuteScalar() as string;
    }

    [Fact]
    public void A_dead_youtube_feed_is_rewritten_to_the_channel_form()
    {
        var id = InsertDeadFeed("UC6Mjg5R5QOJYjrio1JmP8Fg", "oompaville");

        var changed = ChannelMigration.Run(_temp.Database);

        Assert.Equal(1, changed);
        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", AddressOf(id));
    }

    [Fact]
    public void Articles_stay_attached_to_the_feed_they_belong_to()
    {
        var id = InsertDeadFeed("UC6Mjg5R5QOJYjrio1JmP8Fg", "oompaville");
        using (var connection = _temp.Database.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "insert into articles (FeedId, Guid, Title, Link, FetchedAt) values ($f, 'g1', 't', 'l', '2026-01-01')";
            command.Parameters.AddWithValue("$f", id);
            command.ExecuteNonQuery();
        }

        ChannelMigration.Run(_temp.Database);

        using var check = _temp.Database.Open();
        using var count = check.CreateCommand();
        count.CommandText = "select count(*) from articles where FeedId = $f";
        count.Parameters.AddWithValue("$f", id);
        Assert.Equal(1L, (long)count.ExecuteScalar()!);
    }

    [Fact]
    public void Ordinary_feeds_are_left_alone()
    {
        var id = _temp.InsertFeed("https://example.com/feed.xml", "Example");

        var changed = ChannelMigration.Run(_temp.Database);

        Assert.Equal(0, changed);
        Assert.Equal("https://example.com/feed.xml", AddressOf(id));
    }

    [Fact]
    public void Running_it_twice_changes_nothing_the_second_time()
    {
        InsertDeadFeed("UC6Mjg5R5QOJYjrio1JmP8Fg", "oompaville");

        Assert.Equal(1, ChannelMigration.Run(_temp.Database));
        Assert.Equal(0, ChannelMigration.Run(_temp.Database));
    }

    [Fact]
    public void A_row_already_in_the_channel_form_is_untouched()
    {
        var id = _temp.InsertFeed("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", "oompaville");

        Assert.Equal(0, ChannelMigration.Run(_temp.Database));
        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", AddressOf(id));
    }
}