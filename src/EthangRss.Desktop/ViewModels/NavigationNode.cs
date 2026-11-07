using CommunityToolkit.Mvvm.ComponentModel;

namespace EthangRss.Desktop.ViewModels;

/// <summary>One row in the navigation column: either a fixed folder or a feed.</summary>
public sealed partial class NavigationNode : ObservableObject
{
    private NavigationNode(string title, ArticleScopeVm scope, long? feedId, int unreadCount, bool isFolder)
    {
        Title = title;
        Scope = scope;
        FeedId = feedId;
        UnreadCount = unreadCount;
        IsFolder = isFolder;
    }

    public string Title { get; }

    public ArticleScopeVm Scope { get; }

    public long? FeedId { get; }

    public bool IsFolder { get; }

    public string? IconUrl { get; private init; }

    public string? LastError { get; private init; }

    /// <summary>The page this feed belongs to, or null for a folder or a feed with no page.</summary>
    public string? WebAddress { get; private init; }

    [ObservableProperty]
    public partial int UnreadCount { get; set; }

    public bool HasUnread => UnreadCount > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(LastError);

    public static NavigationNode Folder(string title, ArticleScopeVm scope, int unreadCount) =>
        new(title, scope, null, unreadCount, isFolder: true);

    public static NavigationNode Feed(FeedSummaryVm feed) => new(
        feed.Title, ArticleScopeVm.Unread, feed.Id, feed.UnreadCount, isFolder: false)
    {
        IconUrl = feed.IconUrl,
        LastError = feed.LastError,
        WebAddress = feed.WebAddress,
    };

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
    }
}
