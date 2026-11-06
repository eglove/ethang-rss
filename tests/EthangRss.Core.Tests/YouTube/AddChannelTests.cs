using System.Net;
using EthangRss.Core.Feeds;
using EthangRss.Core.Subscriptions;
using EthangRss.Core.YouTube;

namespace EthangRss.Core.Tests.YouTube;

/// <summary>
/// Pasting a channel address is the whole point of the feature for a person: they have a URL
/// from the address bar, not a channel id.
/// </summary>
public sealed class AddChannelTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "YouTube", "Fixtures", "channel-videos.html"));

    private FeedSubscriptions Subscriptions(StubHttpHandler handler) =>
        new(_temp.Database, new FeedSourceRouter(
            new FeedFetcher(new HttpClient(handler)),
            new ChannelFetcher(new HttpClient(handler))));

    private string? StoredAddress()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select XmlAddress from feeds";
        return command.ExecuteScalar() as string;
    }

    [Fact]
    public async Task A_channel_url_is_added_and_stored_under_its_channel_id()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, Fixture());

        var result = await Subscriptions(handler).AddAsync("https://www.youtube.com/@oompaville", CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", StoredAddress());
    }

    [Fact]
    public async Task The_channel_s_videos_arrive_as_articles()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, Fixture());

        await Subscriptions(handler).AddAsync("https://www.youtube.com/@oompaville", CancellationToken.None);

        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        Assert.Equal(3L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public async Task A_channel_id_url_works_too()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, Fixture());

        var result = await Subscriptions(handler)
            .AddAsync("https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg", CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("youtube://channel/UC6Mjg5R5QOJYjrio1JmP8Fg", StoredAddress());
    }

    [Fact]
    public async Task Adding_the_same_channel_twice_says_so_rather_than_duplicating_it()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, Fixture());
        var subscriptions = Subscriptions(handler);

        await subscriptions.AddAsync("https://www.youtube.com/@oompaville", CancellationToken.None);
        var second = await subscriptions.AddAsync("https://www.youtube.com/@oompaville", CancellationToken.None);

        Assert.False(second.Success);
        Assert.Contains("already", second.Message);
    }

    [Fact]
    public async Task A_channel_that_cannot_be_read_reports_why_and_stores_nothing()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "<html>consent</html>");

        var result = await Subscriptions(handler).AddAsync("https://www.youtube.com/@oompaville", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("could not be read", result.Message);
        Assert.Null(StoredAddress());
    }
}