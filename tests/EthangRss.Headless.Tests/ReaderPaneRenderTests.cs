using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EthangRss.Desktop.Services;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>
/// The reader pane is drawn by rebuilding its controls. Marking an article read must not rebuild
/// it: the body has not changed, and tearing the pane down mid-render is how a video player gets
/// destroyed while it is still starting.
/// </summary>
public sealed class ReaderPaneRenderTests
{
    private static readonly DateTimeOffset Published = new(2026, 3, 4, 15, 30, 0, TimeSpan.Zero);

    private sealed class StubService : IReaderService
    {
        public HashSet<long> Read { get; } = [];

        public IReadOnlyList<FeedSummaryVm> GetFeeds() => [new(1, "A feed", null, 0, 2, null)];

        public FolderCountsVm GetFolderCounts() => new(2 - Read.Count, 0, 2);

        public IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit) =>
        [
            new ArticleRowVm(1, "First headline", "A feed", null, Read.Contains(1), false, Published, null),
            new ArticleRowVm(2, "Second headline", "A feed", null, Read.Contains(2), false, Published, null),
        ];

        public ArticleBodyVm? GetArticle(long articleId) =>
            new(articleId, articleId == 1 ? "First headline" : "Second headline",
                "https://example.com/article", "A feed", Read.Contains(articleId), false, Published,
                "<p>body</p>");

        public void MarkRead(long articleId, bool isRead)
        {
            if (isRead)
            {
                Read.Add(articleId);
            }
            else
            {
                Read.Remove(articleId);
            }
        }

        public int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search) => 0;

        public bool ToggleStar(long articleId) => false;

        public void DeleteFeed(long feedId) { }

        public int UnsubscribeFeed(long feedId) => 0;

        public Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PollResultVm(0, 0, 0, 0));
    }

    private static (MainWindow Window, MainViewModel Model, ContentControl Host) Show()
    {
        var model = new MainViewModel(new StubService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var host = window.FindControl<ContentControl>("ReaderHost")!;
        return (window, model, host);
    }

    [AvaloniaFact]
    public void Marking_an_article_read_does_not_rebuild_the_reader_body()
    {
        var (window, model, host) = Show();
        var rendered = host.Content;

        Assert.NotNull(rendered);
        Assert.True(model.Run(ReaderCommand.ToggleRead));
        Dispatcher.UIThread.RunJobs();

        Assert.Same(rendered, host.Content);
        window.Close();
    }

    [AvaloniaFact]
    public void Moving_to_another_article_does_rebuild_the_reader_body()
    {
        var (window, model, host) = Show();
        var rendered = host.Content;

        Assert.True(model.Run(ReaderCommand.NextArticle));
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(rendered, host.Content);
        window.Close();
    }

    [AvaloniaFact]
    public void The_mark_all_button_is_offered_for_the_current_view()
    {
        var (window, _, _) = Show();

        var button = window.GetVisualDescendants()
            .OfType<Button>()
            .SingleOrDefault(candidate => candidate.Content as string == "Mark all read");

        Assert.NotNull(button);
        window.Close();
    }
}