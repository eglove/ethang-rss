namespace EthangRss.Desktop.ViewModels;

/// <summary>
/// Everything the shell needs from the outside world. The view models depend on this rather than
/// on SQLite or HTTP directly, which is what makes folder switching, selection, and key handling
/// testable without a window or a database.
/// </summary>
public interface IReaderService
{
    IReadOnlyList<FeedSummaryVm> GetFeeds();

    FolderCountsVm GetFolderCounts();

    IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit);

    ArticleBodyVm? GetArticle(long articleId);

    /// <summary>Whether opening a teaser article fetches its page. The person's setting, not a constant.</summary>
    bool ReadingModeEnabled { get; }

    /// <summary>
    /// Fetches the article's own page and stores its text, for a feed that only published a teaser.
    /// Returns true when text was recovered and stored. A page that offers nothing is recorded as
    /// tried, so it is not fetched again on the next open.
    /// </summary>
    Task<bool> FetchFullArticleAsync(long articleId, CancellationToken cancellationToken);

    void MarkRead(long articleId, bool isRead);

    /// <summary>Marks read every unread article the given view shows, and returns the count.</summary>
    int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search);

    bool ToggleStar(long articleId);

    void DeleteFeed(long feedId);

    /// <summary>Removes a feed and its articles, and reports how many articles went with it.</summary>
    int UnsubscribeFeed(long feedId);

    Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken);
}
