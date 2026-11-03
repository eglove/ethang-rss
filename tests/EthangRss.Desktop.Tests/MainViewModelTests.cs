using EthangRss.Desktop.Services;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Tests;

public sealed class MainViewModelTests
{
    private static (MainViewModel Model, FakeReaderService Service) Build()
    {
        var service = new FakeReaderService();
        service
            .AddArticle(1, "Alpha one")
            .AddArticle(2, "Alpha two")
            .AddArticle(3, "Beta one", feedId: 2, feedTitle: "Beta feed");

        return (new MainViewModel(service), service);
    }

    [Fact]
    public void Starts_on_the_unread_folder_with_every_article_listed()
    {
        var (model, _) = Build();

        Assert.Equal(ArticleScopeVm.Unread, model.SelectedNode!.Scope);
        Assert.Equal(3, model.Articles.Count);
        Assert.Equal(3, model.UnreadTotal);
    }

    [Fact]
    public void Loading_a_folder_does_not_mark_anything_read()
    {
        var (model, service) = Build();

        Assert.Empty(service.Read);
        Assert.Equal(3, model.UnreadTotal);
        Assert.Equal("Alpha one", model.ReaderTitle);
    }

    [Fact]
    public void Moving_onto_an_article_does_not_change_its_read_state()
    {
        var (model, service) = Build();

        model.Run(ReaderCommand.NextArticle);

        // Moving is navigation, not reading. Nothing is marked read until asked.
        Assert.Equal(2, model.SelectedArticle!.Id);
        Assert.Empty(service.Read);
        Assert.Equal(3, model.UnreadTotal);
    }

    [Fact]
    public void The_read_toggle_label_reflects_the_current_state()
    {
        var (model, _) = Build();

        // The all folder keeps a read article listed, so the cursor stays on it and the label flips.
        model.SelectFolder(ArticleScopeVm.All);
        model.Run(ReaderCommand.NextArticle);

        Assert.Equal("Mark read", model.ReadToggleLabel);

        model.Run(ReaderCommand.ToggleRead);
        Assert.Equal("Mark unread", model.ReadToggleLabel);
    }

    [Fact]
    public void Marking_read_is_the_only_thing_that_changes_the_read_state()
    {
        var (model, service) = Build();

        model.Run(ReaderCommand.NextArticle);
        model.Run(ReaderCommand.NextArticle);
        Assert.Empty(service.Read);

        model.Run(ReaderCommand.ToggleRead);
        Assert.Single(service.Read);
    }

    [Fact]
    public void Switching_to_the_starred_folder_shows_only_starred_articles()
    {
        var (model, service) = Build();
        service.Starred.Add(3);

        model.SelectFolder(ArticleScopeVm.Starred);

        var single = Assert.Single(model.Articles);
        Assert.Equal(3, single.Id);
        Assert.Equal(ArticleScopeVm.Starred, service.LastScope);
    }

    [Fact]
    public void Switching_to_a_feed_filters_the_list()
    {
        var (model, _) = Build();
        var feed = model.Feeds.Single(node => node.FeedId == 2);

        model.SelectedNode = feed;

        var single = Assert.Single(model.Articles);
        Assert.Equal(3, single.Id);
    }

    [Fact]
    public void J_moves_down_and_stops_at_the_last_article()
    {
        var (model, _) = Build();

        Assert.True(model.Run(ReaderCommand.NextArticle));
        Assert.Equal(2, model.SelectedArticle!.Id);

        Assert.True(model.Run(ReaderCommand.NextArticle));
        Assert.Equal(3, model.SelectedArticle!.Id);

        Assert.True(model.Run(ReaderCommand.NextArticle));
        Assert.Equal(3, model.SelectedArticle!.Id);
    }

    [Fact]
    public void K_moves_up_and_stops_at_the_first_article()
    {
        var (model, _) = Build();

        model.Run(ReaderCommand.LastArticle);
        Assert.Equal(3, model.SelectedArticle!.Id);

        model.Run(ReaderCommand.PreviousArticle);
        model.Run(ReaderCommand.PreviousArticle);
        Assert.Equal(1, model.SelectedArticle!.Id);

        model.Run(ReaderCommand.PreviousArticle);
        Assert.Equal(1, model.SelectedArticle!.Id);
    }

    [Fact]
    public void Home_and_end_jump_to_the_edges()
    {
        var (model, _) = Build();

        model.Run(ReaderCommand.LastArticle);
        Assert.Equal(3, model.SelectedArticle!.Id);

        model.Run(ReaderCommand.FirstArticle);
        Assert.Equal(1, model.SelectedArticle!.Id);
    }

    [Fact]
    public void Navigation_on_an_empty_list_is_a_no_op_rather_than_a_crash()
    {
        var service = new FakeReaderService();
        var model = new MainViewModel(service);

        Assert.False(model.Run(ReaderCommand.NextArticle));
        Assert.False(model.Run(ReaderCommand.PreviousArticle));
        Assert.Null(model.SelectedArticle);
    }

    [Fact]
    public void Starring_flips_the_reader_state_and_the_folder_count()
    {
        var (model, service) = Build();

        Assert.True(model.Run(ReaderCommand.ToggleStar));
        Assert.True(model.ReaderIsStarred);
        Assert.Single(service.Starred);

        Assert.True(model.Run(ReaderCommand.ToggleStar));
        Assert.False(model.ReaderIsStarred);
        Assert.Empty(service.Starred);
    }

    [Fact]
    public void Toggling_read_changes_the_articles_state()
    {
        var (model, service) = Build();
        model.SelectFolder(ArticleScopeVm.All);
        model.Run(ReaderCommand.NextArticle);

        Assert.True(model.Run(ReaderCommand.ToggleRead));
        Assert.Contains(2, service.Read);
        Assert.True(model.ReaderIsRead);

        Assert.True(model.Run(ReaderCommand.ToggleRead));
        Assert.DoesNotContain(2, service.Read);
        Assert.False(model.ReaderIsRead);
    }

    [Fact]
    public void Marking_read_moves_to_the_next_article_instead_of_the_top_of_the_list()
    {
        var (model, service) = Build();
        model.Run(ReaderCommand.NextArticle);

        Assert.True(model.Run(ReaderCommand.ToggleRead));

        // The read article leaves the unread folder; the cursor must follow the list, not reset.
        Assert.Contains(2, service.Read);
        Assert.Equal(3, model.SelectedArticle!.Id);
        Assert.Equal("Beta one", model.ReaderTitle);
    }

    [Fact]
    public void Marking_the_last_article_read_lands_on_the_new_last_article()
    {
        var (model, _) = Build();
        model.Run(ReaderCommand.LastArticle);

        Assert.True(model.Run(ReaderCommand.ToggleRead));

        Assert.Equal(2, model.SelectedArticle!.Id);
    }

    [Fact]
    public void Marking_read_keeps_the_article_in_place_in_the_all_folder()
    {
        var (model, service) = Build();
        model.SelectFolder(ArticleScopeVm.All);
        model.Run(ReaderCommand.NextArticle);

        Assert.True(model.Run(ReaderCommand.ToggleRead));

        Assert.Contains(2, service.Read);
        Assert.Equal(2, model.SelectedArticle!.Id);
        Assert.Equal("Alpha two", model.ReaderTitle);
    }

    [Fact]
    public void Marking_every_article_read_in_turn_empties_the_list_and_clears_the_reader()
    {
        var (model, service) = Build();

        // Each read hands the cursor to the next row, so three presses clear the unread folder.
        Assert.True(model.Run(ReaderCommand.ToggleRead));
        Assert.Equal(2, model.SelectedArticle!.Id);
        Assert.True(model.Run(ReaderCommand.ToggleRead));
        Assert.Equal(3, model.SelectedArticle!.Id);
        Assert.True(model.Run(ReaderCommand.ToggleRead));

        Assert.Equal(3, service.Read.Count);
        Assert.Empty(model.Articles);
        Assert.Null(model.SelectedArticle);
        Assert.False(model.HasReaderArticle);
    }

    [Fact]
    public void Mark_all_read_is_still_an_explicit_bulk_action()
    {
        var (model, service) = Build();
        service.Read.Clear();
        model.Load();

        Assert.True(model.Run(ReaderCommand.MarkAllRead));
        Assert.Equal(0, model.UnreadTotal);
        Assert.Contains("Marked 3 read", model.StatusText);
    }

    [Fact]
    public void Open_in_browser_raises_the_request_with_the_article_link()
    {
        var (model, _) = Build();
        string? opened = null;
        model.OpenUrlRequested += (_, url) => opened = url;

        Assert.True(model.Run(ReaderCommand.OpenInBrowser));
        Assert.Equal("https://example.com/1", opened);
    }

    [Fact]
    public void Focus_search_raises_the_focus_request_and_sets_the_flag()
    {
        var (model, _) = Build();
        var focused = false;
        model.FocusSearchRequested += (_, _) => focused = true;

        Assert.True(model.Run(ReaderCommand.FocusSearch));
        Assert.True(model.IsSearchFocused);
        Assert.True(focused);
    }

    [Fact]
    public void Clearing_search_empties_the_term_and_releases_focus()
    {
        var (model, _) = Build();
        model.Run(ReaderCommand.FocusSearch);
        model.SearchText = "Beta";

        Assert.True(model.Run(ReaderCommand.ClearSearch));
        Assert.Equal(string.Empty, model.SearchText);
        Assert.False(model.IsSearchFocused);
        Assert.Equal(3, model.Articles.Count);
    }

    [Fact]
    public void Clearing_an_already_empty_search_reports_nothing_to_do()
    {
        var (model, _) = Build();

        Assert.False(model.Run(ReaderCommand.ClearSearch));
    }

    [Fact]
    public void Search_filters_the_article_list()
    {
        var (model, _) = Build();

        model.SearchText = "Beta";

        var single = Assert.Single(model.Articles);
        Assert.Equal(3, single.Id);
    }

    [Fact]
    public void Shortcut_sheet_toggles()
    {
        var (model, _) = Build();

        Assert.False(model.IsShortcutSheetOpen);
        Assert.True(model.Run(ReaderCommand.ShowShortcuts));
        Assert.True(model.IsShortcutSheetOpen);
        Assert.True(model.Run(ReaderCommand.ShowShortcuts));
        Assert.False(model.IsShortcutSheetOpen);
    }

    [Fact]
    public async Task Refresh_reports_the_poll_result_in_the_status_bar()
    {
        var (model, service) = Build();
        service.NextPoll = new PollResultVm(5, 4, 1, 12);

        await model.RefreshAsync(CancellationToken.None);

        Assert.Contains("Polled 5 feeds", model.StatusText);
        Assert.Contains("12 new", model.StatusText);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task Refreshing_when_nothing_is_due_says_so()
    {
        var (model, _) = Build();

        await model.RefreshAsync(CancellationToken.None);

        Assert.Equal("Nothing was due", model.StatusText);
    }

    [Fact]
    public void Folder_counts_track_unread_starred_and_total()
    {
        var (model, service) = Build();
        service.Starred.Add(2);
        model.Load();

        Assert.Equal(3, model.Folders.Single(node => node.Scope == ArticleScopeVm.Unread).UnreadCount);
        Assert.Equal(1, model.Folders.Single(node => node.Scope == ArticleScopeVm.Starred).UnreadCount);
        Assert.Equal(3, model.Folders.Single(node => node.Scope == ArticleScopeVm.All).UnreadCount);
    }

    [Fact]
    public void Selecting_an_article_that_does_not_exist_leaves_the_reader_blank()
    {
        var (model, _) = Build();

        // A search with no matches must clear the reader rather than leave a stale article.
        model.SearchText = "nothing matches this";

        Assert.Null(model.ReaderTitle);
        Assert.False(model.HasReaderArticle);
    }
}