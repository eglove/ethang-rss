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

    void MarkRead(long articleId, bool isRead);

    /// <summary>Marks read every unread article the given view shows, and returns the count.</summary>
    int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search);

    bool ToggleStar(long articleId);

    void DeleteFeed(long feedId);

    Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken);
}
