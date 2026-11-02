using EthangRss.Core;
using EthangRss.Core.Data;
using EthangRss.Core.Feeds;
using EthangRss.Core.Polling;
using EthangRss.Core.Queries;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Services;

/// <summary>Binds the reader's queries and polling to the view models, and owns the poll loop.</summary>
public sealed class ReaderService : IReaderService, IDisposable
{
    private readonly AppDatabase _database;
    private readonly ReaderQueries _queries;
    private readonly SettingsStore _settings;
    private readonly PollScheduler _scheduler;
    private readonly HttpClient _client;
    private readonly CancellationTokenSource _shutdown = new();

    public ReaderService(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
        _queries = new ReaderQueries(database);
        _settings = new SettingsStore(database);
        _client = new HttpClient();

        var options = new PollOptions
        {
            IntervalMinutes = _settings.GetInt(SettingKeys.RefreshIntervalMinutes, 30),
        };

        _scheduler = new PollScheduler(
            database,
            new FeedFetcher(_client),
            new FeedRepository(database),
            options,
            () => DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<FeedSummaryVm> GetFeeds() =>
        _queries.GetFeeds()
            .Select(feed => new FeedSummaryVm(
                feed.Id, feed.Title, feed.IconUrl, feed.UnreadCount, feed.ArticleCount, feed.LastError))
            .ToList();

    public FolderCountsVm GetFolderCounts()
    {
        var counts = _queries.GetFolderCounts();
        return new FolderCountsVm(counts.Unread, counts.Starred, counts.All);
    }

    public IReadOnlyList<ArticleRowVm> GetArticles(
        ArticleScopeVm scope,
        long? feedId,
        string? search,
        int offset,
        int limit)
    {
        var mapped = scope switch
        {
            ArticleScopeVm.Starred => ArticleScope.Starred,
            ArticleScopeVm.All => ArticleScope.All,
            _ => ArticleScope.Unread,
        };

        return _queries.GetArticles(mapped, feedId, search, offset, limit)
            .Select(article => new ArticleRowVm(
                article.Id,
                article.Title,
                article.FeedTitle,
                article.FeedIconUrl,
                article.IsRead,
                article.IsStarred,
                article.PublishedAt,
                article.Summary))
            .ToList();
    }

    public ArticleBodyVm? GetArticle(long articleId)
    {
        var detail = _queries.GetArticle(articleId);
        return detail is null
            ? null
            : new ArticleBodyVm(
                detail.Id,
                detail.Title,
                detail.Link,
                detail.FeedTitle,
                detail.IsRead,
                detail.IsStarred,
                detail.PublishedAt,
                detail.Content);
    }

    public void MarkRead(long articleId, bool isRead) => _queries.MarkRead(articleId, isRead);

    public int MarkAllRead(long? feedId) => _queries.MarkAllRead(feedId);

    public bool ToggleStar(long articleId) => _queries.ToggleStar(articleId);

    public void DeleteFeed(long feedId) => _queries.DeleteFeed(feedId);

    public async Task<PollResultVm> RefreshAsync(CancellationToken cancellationToken)
    {
        var summary = await _scheduler.RefreshNowAsync(cancellationToken).ConfigureAwait(false);
        _settings.Set(SettingKeys.LastPolledAt, DateTimeOffset.UtcNow.ToString("O"));
        ApplyRetention();

        return new PollResultVm(summary.Polled, summary.Succeeded, summary.Failed, summary.NewArticles);
    }

    /// <summary>Polls once at startup and then on the configured interval until shutdown.</summary>
    public async Task StartPollingAsync(MainViewModel viewModel, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);

        try
        {
            await PollOnceAsync(viewModel, linked.Token).ConfigureAwait(false);

            while (!linked.IsCancellationRequested)
            {
                var minutes = Math.Max(1, _settings.GetInt(SettingKeys.RefreshIntervalMinutes, 30));
                await Task.Delay(TimeSpan.FromMinutes(minutes), linked.Token).ConfigureAwait(false);
                await PollOnceAsync(viewModel, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task PollOnceAsync(MainViewModel viewModel, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _scheduler.RunOnceAsync(cancellationToken).ConfigureAwait(false);
            _settings.Set(SettingKeys.LastPolledAt, DateTimeOffset.UtcNow.ToString("O"));
            ApplyRetention();

            if (summary.Polled > 0)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(viewModel.Load);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A failed pass must never take the app down; the next pass tries again.
        }
    }

    private void ApplyRetention()
    {
        if (_settings.GetBool(SettingKeys.KeepArticlesForever, true))
        {
            return;
        }

        var days = _settings.GetInt(SettingKeys.RetentionDays, 90);
        Core.Retention.ArticleRetention.Apply(_database, days, DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        _client.Dispose();
    }
}
