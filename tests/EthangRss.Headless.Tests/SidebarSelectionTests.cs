using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>
/// The sidebar is two lists over one selection. A list whose item is not in its own collection
/// clears the value it is bound to, so binding both lists to the same property silently undid
/// every feed selection - which is what made unsubscribing impossible to reach from the list.
/// </summary>
public sealed class SidebarSelectionTests
{
    private sealed class StubService : IReaderService
    {
        public IReadOnlyList<FeedSummaryVm> GetFeeds() => [new(7, "A feed", null, 2, 4, null)];

        public FolderCountsVm GetFolderCounts() => new(2, 0, 4);

        public IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit) =>
            [new ArticleRowVm(1, "First headline", "A feed", null, false, false, null, null)];

        public ArticleBodyVm? GetArticle(long articleId) =>
            new(articleId, "First headline", "https://example.com/article", "A feed", false, false, null, "<p>body</p>");

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

    private static (MainWindow Window, MainViewModel Model) Show()
    {
        var model = new MainViewModel(new StubService());
        // Addresses go to a recorder, never to Windows: the default opener would launch the
        // browser of whoever runs the suite.
        var window = new MainWindow(_ => { }) { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, model);
    }

    [AvaloniaFact]
    public void Selecting_a_feed_keeps_it_selected()
    {
        var (window, model) = Show();

        model.SelectedNode = model.Feeds.Single();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.NotNull(model.SelectedNode);
        Assert.Equal(7, model.SelectedNode!.FeedId);

        // And the feed list shows it, so the reader is looking at the feed it says it is.
        var feedList = window.GetVisualDescendants().OfType<ListBox>()
            .Single(list => list.ItemsSource == model.Feeds);
        Assert.Same(model.SelectedNode, feedList.SelectedItem);

        window.Close();
    }

    [AvaloniaFact]
    public void Selecting_a_folder_again_keeps_it_selected()
    {
        var (window, model) = Show();

        model.SelectFolder(ArticleScopeVm.Starred);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(ArticleScopeVm.Starred, model.SelectedNode!.Scope);
        window.Close();
    }
}