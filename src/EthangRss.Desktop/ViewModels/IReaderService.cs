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

    int MarkAllRead(long? feedId);

    bool ToggleStar(long articleId);

    void DeleteFeed(long feedId);

    Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken);
}
