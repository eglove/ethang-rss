using EthangRss.Core.Import;
using EthangRss.Core.Queries;

namespace EthangRss.Core.Tests.Import;

public sealed class SubscriptionImporterTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "dump.sql");

    public void Dispose() => _temp.Dispose();

    private ReaderQueries Queries => new(_temp.Database);

    [Fact]
    public void Reader_keeps_only_feeds_that_have_a_subscription()
    {
        var dump = new D1DumpReader().Read(FixturePath);

        Assert.Equal(3, dump.SubscribedFeeds.Count);
        Assert.DoesNotContain(dump.SubscribedFeeds, feed => feed.Id == "f-orphan");
        Assert.Equal(1, dump.SkippedUnsubscribed);
    }

    [Fact]
    public void Reader_maps_columns_by_name_not_position()
    {
        var dump = new D1DumpReader().Read(FixturePath);
        var additude = dump.SubscribedFeeds.Single(feed => feed.Id == "f-sub-1");

        Assert.Equal("ADDitude", additude.Title);
        Assert.Equal("https://www.additudemag.com/feed/", additude.XmlAddress);
        Assert.Equal("https://www.additudemag.com", additude.Website);
    }

    [Fact]
    public void Reader_decodes_html_entities_in_urls()
    {
        var dump = new D1DumpReader().Read(FixturePath);
        var additude = dump.SubscribedFeeds.Single(feed => feed.Id == "f-sub-1");

        Assert.Contains("&crop=0", additude.IconUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("&#038;", additude.IconUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_handles_escaped_single_quotes()
    {
        var dump = new D1DumpReader().Read(FixturePath);
        var quoted = dump.SubscribedFeeds.Single(feed => feed.Id == "f-quoted");

        Assert.Equal("O'Brien on Code", quoted.Title);
    }

    [Fact]
    public void Import_writes_the_subscribed_feeds_and_nothing_else()
    {
        var result = new SubscriptionImporter(_temp.Database).Import(FixturePath);

        Assert.Equal(3, result.FeedsImported);
        Assert.Equal(1, result.SkippedUnsubscribed);

        var feeds = Queries.GetFeeds();
        Assert.Equal(3, feeds.Count);
        Assert.DoesNotContain(feeds, feed => feed.Title == "Nobody Reads This");
        Assert.Equal(0, Queries.GetFolderCounts().All);
    }

    [Fact]
    public void Import_is_idempotent_by_xml_address()
    {
        var importer = new SubscriptionImporter(_temp.Database);
        importer.Import(FixturePath);

        var second = importer.Import(FixturePath);

        Assert.Equal(0, second.FeedsImported);
        Assert.Equal(3, second.AlreadyPresent);
        Assert.Equal(3, Queries.GetFeeds().Count);
    }
}
