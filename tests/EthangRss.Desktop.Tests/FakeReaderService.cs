using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Tests;

/// <summary>An in-memory stand-in for the reader service, so view-model behaviour is testable
/// without a database, a network, or a window.</summary>
public sealed class FakeReaderService : IReaderService
{
    public Dictionary<long, ArticleBodyVm> Articles { get; } = [];

    public Dictionary<long, FeedSummaryVm> Feeds { get; } = [];

    public HashSet<long> Starred { get; } = [];

    public HashSet<long> Read { get; } = [];

    public List<string> Calls { get; } = [];

    public ArticleScopeVm LastScope { get; private set; }

    public long? LastFeedId { get; private set; }

    public PollResultVm NextPoll { get; set; } = new(0, 0, 0, 0);

    /// <summary>Set to make the next mark-read call fail the way a locked database does.</summary>
    public Exception? MarkReadFailure { get; set; }

    /// <summary>Set to make the next mark-all-read call fail the way a locked database does.</summary>
    public Exception? MarkAllFailure { get; set; }

    /// <summary>Set to make the next unsubscribe call fail the way a locked database does.</summary>
    public Exception? UnsubscribeFailure { get; set; }

    public List<long> Unsubscribed { get; } = [];

    public FakeReaderService AddArticle(
        long id,
        string title,
        long feedId = 1,
        string feedTitle = "A feed",
        bool isRead = false,
        string? webAddress = null)
    {
        Articles[id] = new ArticleBodyVm(id, title, $"https://example.com/{id}", feedTitle, isRead, Starred.Contains(id),
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), "<p>body</p>");
        Feeds.TryAdd(feedId, new FeedSummaryVm(feedId, feedTitle, null, 0, 0, null, webAddress));
        if (isRead)
        {
            Read.Add(id);
        }

        return this;
    }

    public IReadOnlyList<FeedSummaryVm> GetFeeds() =>
        Feeds.Values
            .Select(feed => feed with
            {
                UnreadCount = ArticlesFor(feed.Id).Count(article => !Read.Contains(article.Id)),
                ArticleCount = ArticlesFor(feed.Id).Count(),
            })
            .ToList();

    private IEnumerable<ArticleBodyVm> ArticlesFor(long feedId) =>
        Feeds.TryGetValue(feedId, out var feed)
            ? Articles.Values.Where(article => article.FeedTitle == feed.Title)
            : [];

    public FolderCountsVm GetFolderCounts() =>
        new(
            Articles.Values.Count(article => !Read.Contains(article.Id)),
            Starred.Count,
            Articles.Count);

    public IReadOnlyList<ArticleRowVm> GetArticles(ArticleScopeVm scope, long? feedId, string? search, int offset, int limit)
    {
        LastScope = scope;
        LastFeedId = feedId;
        Calls.Add("GetArticles:" + scope);

        IEnumerable<ArticleBodyVm> source = scope switch
        {
            ArticleScopeVm.Unread => Articles.Values.Where(article => !Read.Contains(article.Id)),
            ArticleScopeVm.Starred => Articles.Values.Where(article => Starred.Contains(article.Id)),
            _ => Articles.Values,
        };

        if (feedId is { } id)
        {
            source = source.Where(article => article.FeedTitle == Feeds[id].Title);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            source = source.Where(article => article.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return source
            .Select(article => new ArticleRowVm(
                article.Id, article.Title, article.FeedTitle, null,
                Read.Contains(article.Id), Starred.Contains(article.Id), article.PublishedAt, null))
            .ToList();
    }

    public ArticleBodyVm? GetArticle(long articleId) =>
        Articles.TryGetValue(articleId, out var article)
            ? article with { IsRead = Read.Contains(articleId), IsStarred = Starred.Contains(articleId) }
            : null;

    public void MarkRead(long articleId, bool isRead)
    {
        Calls.Add("MarkRead:" + articleId + ":" + isRead);
        if (MarkReadFailure is { } failure)
        {
            throw failure;
        }

        if (isRead)
        {
            Read.Add(articleId);
        }
        else
        {
            Read.Remove(articleId);
        }
    }

    public int MarkAllRead(ArticleScopeVm scope, long? feedId, string? search)
    {
        Calls.Add("MarkAllRead:" + scope + ":" + (feedId?.ToString() ?? "all") + ":" + (search ?? string.Empty));
        if (MarkAllFailure is { } failure)
        {
            throw failure;
        }

        var targets = Articles.Keys
            .Where(id => !Read.Contains(id))
            .Where(id => scope != ArticleScopeVm.Starred || Starred.Contains(id))
            .Where(id => feedId is not { } wanted || Articles[id].FeedTitle == Feeds[wanted].Title)
            .Where(id => string.IsNullOrWhiteSpace(search) || Articles[id].Title.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var id in targets)
        {
            Read.Add(id);
        }

        return targets.Count;
    }

    public bool ToggleStar(long articleId)
    {
        Calls.Add("ToggleStar:" + articleId);
        if (!Starred.Remove(articleId))
        {
            Starred.Add(articleId);
            return true;
        }

        return false;
    }

    public void DeleteFeed(long feedId) => Feeds.Remove(feedId);

    public int UnsubscribeFeed(long feedId)
    {
        Calls.Add("UnsubscribeFeed:" + feedId);
        if (UnsubscribeFailure is { } failure)
        {
            throw failure;
        }

        if (!Feeds.TryGetValue(feedId, out var feed))
        {
            return 0;
        }

        var removed = Articles
            .Where(pair => pair.Value.FeedTitle == feed.Title)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var id in removed)
        {
            Articles.Remove(id);
        }

        Feeds.Remove(feedId);
        Unsubscribed.Add(feedId);
        return removed.Count;
    }

    public Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken)
    {
        Calls.Add("Refresh");
        return Task.FromResult(NextPoll);
    }
}
