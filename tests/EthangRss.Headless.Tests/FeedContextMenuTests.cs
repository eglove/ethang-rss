using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>
/// The sidebar's feed rows carry a menu, and the menu's one item has to reach the window's
/// open-a-page path. Built for real here, because a menu declared in a template that never
/// reaches its handler looks exactly like a menu that works.
/// </summary>
public sealed class FeedContextMenuTests
{
    private const string ChannelPage = "https://www.youtube.com/channel/UC6Mjg5R5QOJYjrio1JmP8Fg";

    private sealed class StubService : IReaderService
    {
        public IReadOnlyList<FeedSummaryVm> GetFeeds() => [new(7, "A channel", null, 0, 1, null, ChannelPage)];

        public FolderCountsVm GetFolderCounts() => new(0, 0, 1);

        public IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit) =>
            [new ArticleRowVm(1, "A video", "A channel", null, false, false, null, null)];

        public ArticleBodyVm? GetArticle(long articleId) =>
            new(articleId, "A video", "https://www.youtube.com/watch?v=abc", "A channel", false, false, null, "<p>body</p>");

        public void MarkRead(long articleId, bool isRead) { }

        public int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search) => 0;

        public bool ToggleStar(long articleId) => false;

        public void DeleteFeed(long feedId) { }

        public int UnsubscribeFeed(long feedId) => 0;

        public Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PollResultVm(0, 0, 0, 0));
    }

    /// <summary>A row's menu together with the control it is attached to, which is the only
    /// control a menu can be opened on.</summary>
    private sealed record RowMenu(Control Owner, ContextMenu Menu);

    /// <summary>
    /// A window whose addresses go to the recorder instead of to Windows. Building one with the
    /// default opener is what made these tests launch the browser of whoever ran them.
    /// </summary>
    private static (MainWindow Window, MainViewModel Model) Show(Action<string> openUrl)
    {
        var model = new MainViewModel(new StubService());
        var window = new MainWindow(openUrl) { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, model);
    }

    private static RowMenu? FindRowMenu(MainWindow window, object itemsSource)
    {
        var list = window.GetVisualDescendants().OfType<ListBox>().Single(box => box.ItemsSource == itemsSource);
        var row = list.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        foreach (var control in row.GetVisualDescendants().OfType<Control>())
        {
            if (control.ContextMenu is { } menu)
            {
                return new RowMenu(control, menu);
            }
        }

        return null;
    }

    [AvaloniaFact]
    public void A_feed_row_offers_opening_its_page()
    {
        string? opened = null;
        var (window, model) = Show(url => opened = url);

        var found = FindRowMenu(window, model.Feeds);
        Assert.NotNull(found);

        // Opened the way a right-click opens it, because the row's item is what the menu's
        // handler reads: a menu that never opened would pass a test that only counts items.
        found!.Menu.Open(found.Owner);
        Dispatcher.UIThread.RunJobs();

        var item = found.Menu.Items.OfType<MenuItem>().Single();
        Assert.Equal("Open feed page", item.Header as string);

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal(ChannelPage, opened);

        window.Close();
    }

    [AvaloniaFact]
    public void A_folder_row_has_no_page_to_open()
    {
        var (window, model) = Show(_ => { });

        Assert.Null(FindRowMenu(window, model.Folders));

        window.Close();
    }
}