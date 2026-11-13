using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EthangRss.Core.Formatting;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>Renders the real article card, so the timestamp's presence and its corner are facts
/// about the window rather than about a binding written in a file.</summary>
public sealed class ArticleCardHeadlessTests
{
    private static readonly DateTimeOffset Published = new(2026, 3, 4, 15, 30, 0, TimeSpan.Zero);

    private sealed class StubService : IReaderService
    {
        public IReadOnlyList<FeedSummaryVm> GetFeeds() => [new(1, "A feed", null, 0, 1, null)];

        public FolderCountsVm GetFolderCounts() => new(1, 0, 1);

        public IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit) =>
            [new ArticleRowVm(1, "A headline", "A feed", null, false, false, Published, null)];

        public ArticleBodyVm? GetArticle(long articleId) =>
            new(articleId, "A headline", "https://example.com/1", "A feed", false, false, Published, "<p>body</p>");

        public void MarkRead(long articleId, bool isRead) { }

        public int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search) => 0;

        public bool ToggleStar(long articleId) => false;

        public void DeleteFeed(long feedId) { }

        public int UnsubscribeFeed(long feedId) => 0;

        public Task<bool> FetchFullArticleAsync(long articleId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public bool ReadingModeEnabled => true;

        public Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PollResultVm(0, 0, 0, 0));
    }

    private static (MainWindow Window, ListBox List) Show()
    {
        // Addresses go to a recorder, never to Windows: the default opener would launch the
        // browser of whoever runs the suite.
        var window = new MainWindow(_ => { }) { DataContext = new MainViewModel(new StubService()) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var list = window.GetVisualDescendants()
            .OfType<ListBox>()
            .Single(box => box.Classes.Contains("articles"));

        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return (window, list);
    }

    [AvaloniaFact]
    public void The_card_shows_the_timestamp_in_the_locale_format()
    {
        var (window, list) = Show();

        var labels = list.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.Contains(LocaleDate.Format(Published), labels);
        window.Close();
    }

    [AvaloniaFact]
    public void The_timestamp_sits_at_the_bottom_right_of_the_card()
    {
        var (window, list) = Show();

        var card = list.GetVisualDescendants().OfType<ListBoxItem>().First();
        var blocks = card.GetVisualDescendants().OfType<TextBlock>().ToList();

        var stamp = blocks.Single(block => block.Text == LocaleDate.Format(Published));
        var title = blocks.Single(block => block.Text == "A headline");

        // Bounds are parent-relative, so both are translated into the card's own coordinates.
        var stampOrigin = stamp.TranslatePoint(new Point(0, 0), card)!.Value;
        var titleOrigin = title.TranslatePoint(new Point(0, 0), card)!.Value;
        var cardBounds = card.Bounds;

        var stampRight = stampOrigin.X + stamp.Bounds.Width;

        // Right of the card's midpoint, below the title, and hard against its right edge.
        Assert.True(stampRight > cardBounds.Width / 2, "timestamp is not on the right");
        Assert.True(stampOrigin.Y > titleOrigin.Y, "timestamp is not below the title");
        Assert.True(cardBounds.Width - stampRight < 24, "timestamp is not flush with the right edge");

        window.Close();
    }
}
