using EthangRss.Desktop.Services;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Tests;

/// <summary>
/// Reading mode is a fetch that happens because a person opened an article. These tests pin the
/// three things that make it acceptable: it runs once per article, it does not run for a body that
/// is already an article, and a failure costs a status line rather than the pane.
/// </summary>
public sealed class ReadingModeViewModelTests
{
    private static (MainViewModel Model, FakeReaderService Service) Build()
    {
        var service = new FakeReaderService();
        service.AddArticle(1, "Teaser article");
        return (new MainViewModel(service), service);
    }

    [Fact]
    public void Opening_a_teaser_article_fetches_its_page_once()
    {
        var (model, service) = Build();

        Assert.Equal(1, Assert.Single(service.FullContentFetches));
        Assert.Equal("<p>The whole article.</p>", model.ReaderBody);
    }

    [Fact]
    public void Opening_the_same_article_again_does_not_fetch_it_again()
    {
        var (model, service) = Build();

        model.Run(ReaderCommand.NextArticle);
        model.Run(ReaderCommand.PreviousArticle);

        Assert.Single(service.FullContentFetches);
    }

    [Fact]
    public void An_article_the_feed_already_carries_in_full_is_not_fetched()
    {
        var service = new FakeReaderService();
        service.AddArticle(1, "Full article", content: new string('x', 4000));

        var model = new MainViewModel(service);

        Assert.Empty(service.FullContentFetches);
        Assert.Equal(new string('x', 4000), model.ReaderBody);
    }

    [Fact]
    public void A_page_that_offers_nothing_leaves_the_teaser_and_says_so()
    {
        var service = new FakeReaderService { FullContentResult = false };
        service.AddArticle(1, "Teaser article");

        var model = new MainViewModel(service);

        Assert.Equal("<p>body</p>", model.ReaderBody);
        Assert.Equal("This article's page had no more text than the feed did.", model.ReaderNote);
    }

    [Fact]
    public void A_fetch_that_throws_costs_a_status_line_not_the_pane()
    {
        var service = new FakeReaderService { FullContentFailure = new HttpRequestException("no network") };
        service.AddArticle(1, "Teaser article");

        var model = new MainViewModel(service);

        Assert.Equal("Teaser article", model.ReaderTitle);
        Assert.Equal("<p>body</p>", model.ReaderBody);
        Assert.Contains("no network", model.ReaderNote!, StringComparison.Ordinal);
    }

    [Fact]
    public void Turning_reading_mode_off_leaves_the_teaser_alone()
    {
        var service = new FakeReaderService { ReadingModeEnabled = false };
        service.AddArticle(1, "Teaser article");

        _ = new MainViewModel(service);

        Assert.Empty(service.FullContentFetches);
    }

    [Fact]
    public void A_video_article_is_not_fetched_for_reading_mode()
    {
        // A video article is watched, not read: its page is a player, and the extractor would
        // return the description the feed already carried.
        var service = new FakeReaderService();
        service.AddArticle(1, "A video", link: "https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        var model = new MainViewModel(service);

        Assert.Empty(service.FullContentFetches);
    }
}