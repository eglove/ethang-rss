namespace EthangRss.Desktop.ViewModels;

public enum ArticleScopeVm
{
    Unread,
    All,
    Starred,
}

/// <summary>
/// A feed as the sidebar shows it. WebAddress is the page a browser should open for it, which is
/// not the address the app fetches: a channel is stored under the app's own scheme.
/// </summary>
public sealed record FeedSummaryVm(
    long Id,
    string Title,
    string? IconUrl,
    int UnreadCount,
    int ArticleCount,
    string? LastError,
    string? WebAddress = null);

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

/// <param name="FullContent">
/// The article text recovered from the page itself, or null when reading mode has not run for
/// this article. An empty string means it ran and the page offered no prose.
/// </param>
public sealed record ArticleBodyVm(
    long Id,
    string Title,
    string Link,
    string FeedTitle,
    bool IsRead,
    bool IsStarred,
    DateTimeOffset? PublishedAt,
    string RawContent,
    string? FullContent = null)
{
    /// <summary>True when reading mode has already run for this article, whatever it found.</summary>
    public bool FullContentAttempted => FullContent is not null;

    /// <summary>The body to draw: the recovered article when there is one, the feed's body otherwise.</summary>
    public string PreferredContent =>
        FullContent is { Length: > 0 } recovered ? recovered : RawContent;

    /// <summary>
    /// True when the feed only gave a teaser, which is the case reading mode exists for. A body
    /// that is already an article is left alone: fetching the page would cost a request to show
    /// what is already on screen.
    /// </summary>
    public bool NeedsFullContent => !FullContentAttempted && RawContent.Length < TeaserLength;

    /// <summary>
    /// What counts as a teaser. Measured against the feeds in this reader: a feed that publishes the
    /// article stores thousands of characters, and one that publishes a sentence stores a few
    /// hundred. The gap between them is wide, so the exact threshold is not delicate.
    /// </summary>
    public const int TeaserLength = 600;
}

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
