namespace EthangRss.Desktop.ViewModels;

public enum ArticleScopeVm
{
    Unread,
    All,
    Starred,
}

public sealed record FeedSummaryVm(
    long Id,
    string Title,
    string? IconUrl,
    int UnreadCount,
    int ArticleCount,
    string? LastError);

public sealed record FolderCountsVm(int Unread, int Starred, int All);

public sealed record ArticleRowVm(
    long Id,
    string Title,
    string FeedTitle,
    string? FeedIconUrl,
    bool IsRead,
    bool IsStarred,
    DateTimeOffset? PublishedAt,
    string? Summary);

public sealed record ArticleBodyVm(
    long Id,
    string Title,
    string Link,
    string FeedTitle,
    bool IsRead,
    bool IsStarred,
    DateTimeOffset? PublishedAt,
    string RawContent);

public sealed record PollResultVm(int Polled, int Succeeded, int Failed, int NewArticles);
