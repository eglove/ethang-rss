using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>
/// Unsubscribing is the one action in the reader that throws work away, so the button that starts
/// it and the dialog that asks about it are built for real here: a missing binding or a dialog
/// that cannot be dismissed would otherwise only show up in front of a person.
/// </summary>
public sealed class UnsubscribeHeadlessTests
{
    private sealed class StubService : IReaderService
    {
        public Dictionary<long, FeedSummaryVm> Feeds { get; } = new()
        {
            [7] = new FeedSummaryVm(7, "A feed", null, 2, 4, null),
        };

        public IReadOnlyList<FeedSummaryVm> GetFeeds() => Feeds.Values.ToList();

        public FolderCountsVm GetFolderCounts() => new(2, 0, 4);

        public IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit) =>
        [
            new ArticleRowVm(1, "First headline", "A feed", null, false, false, null, null),
        ];

        public ArticleBodyVm? GetArticle(long articleId) =>
            new(articleId, "First headline", "https://example.com/article", "A feed", false, false, null, "<p>body</p>");

        public void MarkRead(long articleId, bool isRead) { }

        public int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search) => 0;

        public bool ToggleStar(long articleId) => false;

        public void DeleteFeed(long feedId) => Feeds.Remove(feedId);

        public int UnsubscribeFeed(long feedId) => Feeds.Remove(feedId) ? 4 : 0;

        public Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PollResultVm(0, 0, 0, 0));
    }

    private static (MainWindow Window, MainViewModel Model) Show()
    {
        var model = new MainViewModel(new StubService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, model);
    }

    private static Button? UnsubscribeButton(Window window) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .SingleOrDefault(candidate => candidate.Content as string == "Unsubscribe");

    [AvaloniaFact]
    public void The_unsubscribe_button_is_offered_only_while_a_feed_is_selected()
    {
        var (window, model) = Show();
        var button = UnsubscribeButton(window);

        Assert.NotNull(button);
        Assert.False(button!.IsVisible);

        model.SelectedNode = model.Feeds.Single();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(button.IsVisible);
        Assert.Contains("Delete", ToolTip.GetTip(button) as string ?? string.Empty, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public void The_confirmation_names_the_feed_and_how_much_it_takes_with_it()
    {
        var dialog = new UnsubscribeDialog("A feed", 4);
        dialog.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        dialog.UpdateLayout();

        var texts = dialog.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();
        Assert.Contains(texts, text => text is not null && text.Contains("A feed", StringComparison.Ordinal));
        Assert.Contains(texts, text => text is not null && text.Contains("4 stored articles", StringComparison.Ordinal));

        var buttons = dialog.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string).ToList();
        Assert.Contains("Keep feed", buttons);
        Assert.Contains("Unsubscribe", buttons);

        // Nothing is removed until the person says so.
        Assert.False(dialog.IsConfirmed);
        dialog.Close();
    }
}