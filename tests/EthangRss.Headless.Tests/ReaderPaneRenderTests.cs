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

        // A body long enough to be an article rather than a teaser: reading mode only runs for a
        // feed that published a sentence, so this is the ordinary case where there is nothing to
        // report about the article on screen.
        public ArticleBodyVm? GetArticle(long articleId) =>
            new(articleId, articleId == 1 ? "First headline" : "Second headline",
                "https://example.com/article", "A feed", Read.Contains(articleId), false, Published,
                "<p>" + new string('x', 4000) + "</p>");

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

        public Task<bool> FetchFullArticleAsync(long articleId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public bool ReadingModeEnabled => true;

        public Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PollResultVm(0, 0, 0, 0));
    }

    private static (MainWindow Window, MainViewModel Model, ContentControl Host) Show()
    {
        var model = new MainViewModel(new StubService());
        // Addresses go to a recorder, never to Windows: the default opener would launch the
        // browser of whoever runs the suite.
        var window = new MainWindow(_ => { }) { DataContext = model };
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
    public void The_pane_shows_the_note_reading_mode_left()
    {
        var (window, model, _) = Show();

        // A page that gave no more text than the feed did has to say so, or the article simply
        // looks shorter than it is with no explanation.
        model.ReaderNote = "This article's page had no more text than the feed did.";
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var shown = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Any(block => block.Text == "This article's page had no more text than the feed did." && block.IsVisible);

        Assert.True(shown);
        window.Close();
    }

    [AvaloniaFact]
    public void The_note_is_hidden_when_there_is_nothing_to_report()
    {
        var (window, _, _) = Show();

        var note = window.FindControl<TextBlock>("ReaderNote");

        // Nothing to report means the note is not drawn at all, rather than drawn empty: an empty
        // line above the article would be a gap with no explanation.
        Assert.NotNull(note);
        Assert.Null(note.Text);
        Assert.False(note.IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void The_article_column_is_held_to_a_readable_measure()
    {
        var (window, _, host) = Show();

        // A full-width line on a wide window is hard to track back from. The column is capped at
        // roughly 65 characters so the text keeps a readable measure whatever the window does.
        var column = host.Parent as Control;
        Assert.NotNull(column);
        Assert.True(column!.MaxWidth > 0 && column.MaxWidth < 1000,
            $"expected a capped reading measure, got MaxWidth={column.MaxWidth}");

        window.Width = 1600;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(column.Bounds.Width <= column.MaxWidth + 1,
            $"column grew past its measure: {column.Bounds.Width} > {column.MaxWidth}");
        window.Close();
    }

    [AvaloniaFact]
    public void The_title_is_held_to_the_same_measure_as_the_body()
    {
        var (window, model, host) = Show();

        // A heading that runs wider than the text under it reads as a mistake, so the two share
        // one measure rather than each carrying its own number.
        var column = (Control)host.Parent!;
        var heading = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Classes.Contains("article-title"));
        var headingColumn = (Control)heading.Parent!;

        Assert.True(headingColumn.MaxWidth > 0, "the title has no measure of its own");
        Assert.Equal(column.MaxWidth, headingColumn.MaxWidth);
        window.Close();
    }

    [AvaloniaFact]
    public void The_title_does_not_grow_past_its_measure_when_the_window_widens()
    {
        var (window, _, _) = Show();

        var heading = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .First(block => block.Classes.Contains("article-title"));
        var headingColumn = (Control)heading.Parent!;

        window.Width = 1900;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(headingColumn.Bounds.Width <= headingColumn.MaxWidth + 1,
            $"title grew past its measure: {headingColumn.Bounds.Width} > {headingColumn.MaxWidth}");
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