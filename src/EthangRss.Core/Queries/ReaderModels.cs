namespace EthangRss.Core.Queries;

/// <summary>Which slice of the library a list is showing.</summary>
public enum ArticleScope
{
    Unread,
    All,
    Starred,
}

/// <summary>A feed in the navigation column.</summary>
public sealed record FeedSummary(
    long Id,
    string Title,
    string XmlAddress,
    string? Website,
    string? IconUrl,
    int UnreadCount,
    int ArticleCount,
    string? LastError,
    string? LastFetchedAt,
    int? FetchIntervalMinutes);

/// <summary>A row in the article list.</summary>
public sealed record ArticleListItem(
    long Id,
    string Guid,
    string Title,
    string Link,
    string FeedTitle,
    string? FeedIconUrl,
    long FeedId,
    bool IsRead,
    bool IsStarred,
    DateTimeOffset? PublishedAt,
    string? Summary);

/// <summary>A full article for the reader pane.</summary>
public sealed record ArticleDetail(
    long Id,
    string Title,
    string Link,
    string Content,
    string FeedTitle,
    long FeedId,
    bool IsRead,
    bool IsStarred,
    DateTimeOffset? PublishedAt);

/// <summary>The unread and starred totals shown beside the fixed folders.</summary>
public sealed record FolderCounts(int Unread, int Starred, int All);
