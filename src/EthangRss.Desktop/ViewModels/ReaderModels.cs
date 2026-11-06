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
    string? Summary)
{
    /// <summary>The timestamp as the machine's locale writes it, or empty when the feed had none.</summary>
    public string PublishedLabel => EthangRss.Core.Formatting.LocaleDate.Format(PublishedAt);

    public bool HasPublishedLabel => PublishedAt is not null;
}

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

/// <summary>
/// The question asked before a feed is thrown away. The view model raises it and then reads the
/// answer, so the decision lives in the window while the consequence stays testable without one.
/// </summary>
public sealed class UnsubscribePrompt
{
    public UnsubscribePrompt(long feedId, string feedTitle, int articleCount)
    {
        FeedId = feedId;
        FeedTitle = feedTitle;
        ArticleCount = articleCount;
    }

    public long FeedId { get; }

    public string FeedTitle { get; }

    public int ArticleCount { get; }

    /// <summary>Set by whoever asked the question. Left false, nothing is removed.</summary>
    public bool IsConfirmed { get; set; }
}
