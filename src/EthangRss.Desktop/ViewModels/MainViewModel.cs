using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EthangRss.Desktop.Services;

namespace EthangRss.Desktop.ViewModels;

/// <summary>
/// The whole reader: navigation, article list, reader pane, and keyboard routing. It talks to
/// IReaderService only, so every behaviour below is exercised by tests without a UI.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IReaderService _service;
    private bool _suppressSelection;
    private long? _suppressedReadFor;

    public MainViewModel(IReaderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;

        Folders.Add(NavigationNode.Folder("Unread", ArticleScopeVm.Unread, 0));
        Folders.Add(NavigationNode.Folder("Starred", ArticleScopeVm.Starred, 0));
        Folders.Add(NavigationNode.Folder("All", ArticleScopeVm.All, 0));

        SelectedNode = Folders[0];
        Load();
    }

    public ObservableCollection<NavigationNode> Folders { get; } = [];

    public ObservableCollection<NavigationNode> Feeds { get; } = [];

    public ObservableCollection<ArticleRowVm> Articles { get; } = [];

    [ObservableProperty]
    public partial NavigationNode? SelectedNode { get; set; }

    [ObservableProperty]
    public partial ArticleRowVm? SelectedArticle { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearchFocused { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsShortcutSheetOpen { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial string? ReaderTitle { get; set; }

    [ObservableProperty]
    public partial string? ReaderMeta { get; set; }

    [ObservableProperty]
    public partial string? ReaderBody { get; set; }

    [ObservableProperty]
    public partial string? ReaderLink { get; set; }

    [ObservableProperty]
    public partial bool ReaderIsStarred { get; set; }

    [ObservableProperty]
    public partial bool ReaderIsRead { get; set; }

    [ObservableProperty]
    public partial int UnreadTotal { get; set; }

    public bool IsEmptyFolder => Articles.Count == 0;

    public bool HasReaderArticle => !string.IsNullOrEmpty(ReaderTitle);

    /// <summary>The explicit read toggle's label, which follows the article's actual state.</summary>
    public string ReadToggleLabel => ReaderIsRead ? "Mark unread" : "Mark read";

    partial void OnReaderIsReadChanged(bool value) => OnPropertyChanged(nameof(ReadToggleLabel));

    /// <summary>Re-reads the current folder so a changed read state shows up in the list.</summary>
    public void RefreshArticleList() => LoadArticles();

    public IReadOnlyList<(string Keys, string Description)> Shortcuts => KeyboardMap.CheatSheet;

    public event EventHandler<string>? OpenUrlRequested;

    public event EventHandler? FocusSearchRequested;

    public void Load()
    {
        LoadNavigation();
        LoadArticles();
    }

    private void LoadNavigation()
    {
        var counts = _service.GetFolderCounts();
        UnreadTotal = counts.Unread;

        var folderValues = new[] { counts.Unread, counts.Starred, counts.All };
        for (var index = 0; index < Folders.Count; index++)
        {
            Folders[index].UnreadCount = folderValues[index];
        }

        var selectedFeed = SelectedNode?.FeedId;
        Feeds.Clear();
        foreach (var feed in _service.GetFeeds())
        {
            Feeds.Add(NavigationNode.Feed(feed));
        }

        if (selectedFeed is { } id)
        {
            SelectedNode = Feeds.FirstOrDefault(node => node.FeedId == id) ?? SelectedNode;
        }
    }

    private void LoadArticles()
    {
        _suppressSelection = true;
        try
        {
            Articles.Clear();
            var scope = SelectedNode?.Scope ?? ArticleScopeVm.Unread;
            foreach (var row in _service.GetArticles(scope, SelectedNode?.FeedId, SearchText, 0, 200))
            {
                Articles.Add(row);
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        OnPropertyChanged(nameof(IsEmptyFolder));
        ShowArticle(Articles.FirstOrDefault());
    }

    /// <summary>
    /// Shows an article. Selection never changes read state: marking read is an explicit act, by the
    /// button, the m key, or Shift+A. This is the single place the reader pane is filled.
    /// </summary>
    private void ShowArticle(ArticleRowVm? row)
    {
        _suppressSelection = true;
        try
        {
            SelectedArticle = row;
        }
        finally
        {
            _suppressSelection = false;
        }

        if (row is null)
        {
            ReaderTitle = null;
            ReaderMeta = null;
            ReaderBody = null;
            ReaderLink = null;
            ReaderIsStarred = false;
            ReaderIsRead = false;
            OnPropertyChanged(nameof(HasReaderArticle));
            return;
        }

        var detail = _service.GetArticle(row.Id);
        if (detail is null)
        {
            return;
        }

        ReaderTitle = detail.Title;
        ReaderMeta = detail.PublishedAt is null
            ? detail.FeedTitle
            : detail.FeedTitle + "  |  " + detail.PublishedAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
        ReaderBody = detail.RawContent;
        ReaderLink = detail.Link;
        ReaderIsStarred = detail.IsStarred;
        ReaderIsRead = detail.IsRead;
        OnPropertyChanged(nameof(HasReaderArticle));

    }

    partial void OnSelectedNodeChanged(NavigationNode? value)
    {
        if (value is not null)
        {
            LoadArticles();
        }
    }

    partial void OnSelectedArticleChanged(ArticleRowVm? value)
    {
        if (value is not null && value.Id != _suppressedReadFor)
        {
            _suppressedReadFor = null;
        }

        if (!_suppressSelection && value is not null)
        {
            ShowArticle(value);
        }
    }

    partial void OnSearchTextChanged(string value) => LoadArticles();

    /// <summary>Runs a keyboard command. Returns false when the command does not apply.</summary>
    public bool Run(ReaderCommand command)
    {
        switch (command)
        {
            case ReaderCommand.NextArticle:
                return Move(1);
            case ReaderCommand.PreviousArticle:
                return Move(-1);
            case ReaderCommand.FirstArticle:
                return MoveTo(0);
            case ReaderCommand.LastArticle:
                return MoveTo(Articles.Count - 1);
            case ReaderCommand.OpenInBrowser:
                return OpenCurrent();
            case ReaderCommand.ToggleStar:
                return StarCurrent();
            case ReaderCommand.ToggleRead:
                return ToggleReadCurrent();
            case ReaderCommand.MarkAllRead:
                return MarkAllRead();
            case ReaderCommand.FocusSearch:
                IsSearchFocused = true;
                FocusSearchRequested?.Invoke(this, EventArgs.Empty);
                return true;
            case ReaderCommand.ClearSearch:
                IsSearchFocused = false;
                if (SearchText.Length == 0)
                {
                    return false;
                }

                SearchText = string.Empty;
                return true;
            case ReaderCommand.Refresh:
                _ = RefreshAsync(CancellationToken.None);
                return true;
            case ReaderCommand.ShowShortcuts:
                IsShortcutSheetOpen = !IsShortcutSheetOpen;
                return true;
            case ReaderCommand.GoToUnread:
                return SelectFolder(ArticleScopeVm.Unread);
            case ReaderCommand.GoToAll:
                return SelectFolder(ArticleScopeVm.All);
            case ReaderCommand.GoToStarred:
                return SelectFolder(ArticleScopeVm.Starred);
            default:
                return false;
        }
    }

    public bool SelectFolder(ArticleScopeVm scope)
    {
        var folder = Folders.FirstOrDefault(node => node.Scope == scope);
        if (folder is null)
        {
            return false;
        }

        SelectedNode = folder;
        return true;
    }

    private bool Move(int delta)
    {
        if (Articles.Count == 0)
        {
            return false;
        }

        var current = SelectedArticle is null
            ? (delta >= 0 ? -1 : Articles.Count)
            : Articles.IndexOf(SelectedArticle);

        var next = Math.Clamp(current + delta, 0, Articles.Count - 1);

        SelectedArticle = Articles[next];
        return true;
    }

    private bool MoveTo(int index)
    {
        if (Articles.Count == 0)
        {
            return false;
        }

        SelectedArticle = Articles[Math.Clamp(index, 0, Articles.Count - 1)];
        return true;
    }

    private bool OpenCurrent()
    {
        if (ReaderLink is not { Length: > 0 } link)
        {
            return false;
        }

        OpenUrlRequested?.Invoke(this, link);
        return true;
    }

    private bool StarCurrent()
    {
        if (SelectedArticle is null)
        {
            return false;
        }

        ReaderIsStarred = _service.ToggleStar(SelectedArticle.Id);
        LoadNavigation();
        return true;
    }

    private bool ToggleReadCurrent()
    {
        if (SelectedArticle is null)
        {
            return false;
        }

        // The row in the list is a snapshot; the row that counts is the stored one.
        var current = _service.GetArticle(SelectedArticle.Id)?.IsRead ?? SelectedArticle.IsRead;
        var next = !current;
        _service.MarkRead(SelectedArticle.Id, next);
        ReaderIsRead = next;

        // Remember the explicit choice so re-selecting the row cannot silently reverse it.
        _suppressedReadFor = next ? null : SelectedArticle.Id;

        // Only the counts change; reloading the list would re-select the article and undo this
        // by marking it read again on selection.
        LoadNavigation();
        return true;
    }

    private bool MarkAllRead()
    {
        var affected = _service.MarkAllRead(SelectedNode?.FeedId);
        if (affected == 0)
        {
            return false;
        }

        StatusText = "Marked " + affected + " read";
        Load();
        return true;
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "Refreshing...";
        try
        {
            var result = await _service.RefreshAsync(cancellationToken).ConfigureAwait(true);
            StatusText = result.Polled == 0
                ? "Nothing was due"
                : "Polled " + result.Polled + " feeds, " + result.NewArticles + " new, " + result.Failed + " failed";
            Load();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Refresh cancelled";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
