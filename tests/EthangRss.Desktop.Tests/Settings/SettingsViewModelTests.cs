using EthangRss.Core;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Tests.Settings;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly SettingsStore _store;
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    public SettingsViewModelTests() => _store = new SettingsStore(_temp.Database);

    public void Dispose() => _temp.Dispose();

    private SettingsViewModel NewModel() => new(_store, _temp.Database, () => Now);

    private void SeedArticle(string guid, DateTimeOffset published, bool starred = false, bool isRead = false)
    {
        var feed = _temp.InsertFeed("https://e.com/" + guid, "F" + guid);
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into articles (FeedId, Guid, Title, Link, PublishedAt, FetchedAt, IsRead, IsStarred) " +
            "values ($feed, $guid, $guid, 'https://e.com/a', $published, $published, $read, $starred)";
        command.Parameters.AddWithValue("$feed", feed);
        command.Parameters.AddWithValue("$guid", guid);
        command.Parameters.AddWithValue("$published", published.ToString("O"));
        command.Parameters.AddWithValue("$read", isRead ? 1 : 0);
        command.Parameters.AddWithValue("$starred", starred ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private long CountArticles()
    {
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from articles";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void Defaults_match_the_spec()
    {
        var model = NewModel();

        Assert.Equal(30, model.RefreshIntervalMinutes);
        Assert.True(model.KeepUnreadForever);
        Assert.True(model.KeepReadForever);
        Assert.True(model.FilterYoutubeShorts);
        Assert.False(model.NotificationsEnabled);
        Assert.False(model.StartWithWindows);
    }

    [Fact]
    public void Saving_persists_every_value()
    {
        var model = NewModel();
        model.RefreshIntervalMinutes = 45;
        model.KeepUnreadForever = false;
        model.UnreadRetentionDays = 30;
        model.KeepReadForever = false;
        model.ReadRetentionDays = 7;
        model.FilterYoutubeShorts = false;
        model.NotificationsEnabled = true;
        model.NotificationThreshold = 3;
        model.StartWithWindows = true;

        model.Save();

        var reloaded = NewModel();
        Assert.Equal(45, reloaded.RefreshIntervalMinutes);
        Assert.False(reloaded.KeepUnreadForever);
        Assert.Equal(30, reloaded.UnreadRetentionDays);
        Assert.False(reloaded.KeepReadForever);
        Assert.Equal(7, reloaded.ReadRetentionDays);
        Assert.False(reloaded.FilterYoutubeShorts);
        Assert.True(reloaded.NotificationsEnabled);
        Assert.Equal(3, reloaded.NotificationThreshold);
        Assert.True(reloaded.StartWithWindows);
    }

    [Fact]
    public void Each_retention_input_is_only_meaningful_when_its_own_forever_box_is_off()
    {
        var model = NewModel();

        Assert.False(model.UnreadRetentionEnabled);
        Assert.False(model.ReadRetentionEnabled);

        model.KeepUnreadForever = false;
        Assert.True(model.UnreadRetentionEnabled);
        Assert.False(model.ReadRetentionEnabled);

        model.KeepReadForever = false;
        Assert.True(model.ReadRetentionEnabled);
    }

    [Fact]
    public void Applying_retention_while_both_windows_are_off_deletes_nothing()
    {
        SeedArticle("old", Now.AddDays(-500));
        var model = NewModel();

        Assert.Equal(0, model.ApplyRetentionNow());
        Assert.Equal(1, CountArticles());
    }

    [Fact]
    public void Applying_retention_deletes_only_articles_outside_the_window()
    {
        SeedArticle("old", Now.AddDays(-400));
        SeedArticle("new", Now.AddDays(-2));
        SeedArticle("starred", Now.AddDays(-400), starred: true);
        var model = NewModel();
        model.KeepUnreadForever = false;
        model.UnreadRetentionDays = 90;

        Assert.Equal(1, model.ApplyRetentionNow());
        Assert.Equal(2, CountArticles());
        Assert.Contains("Deleted 1 old article", model.StatusText);
    }

    [Fact]
    public void Read_and_unread_keep_their_own_windows()
    {
        SeedArticle("unread-old", Now.AddDays(-400));
        SeedArticle("read-old", Now.AddDays(-400), isRead: true);
        SeedArticle("read-new", Now.AddDays(-3), isRead: true);

        var model = NewModel();
        model.KeepUnreadForever = false;
        model.UnreadRetentionDays = 3650;
        model.KeepReadForever = false;
        model.ReadRetentionDays = 30;

        // Only the read article past 30 days goes; the unread one is inside its own 10-year window.
        Assert.Equal(1, model.ApplyRetentionNow());
        Assert.Equal(2, CountArticles());
    }

    [Fact]
    public void The_database_path_is_shown_so_the_folder_can_be_opened()
    {
        Assert.Equal(_temp.Database.DatabasePath, NewModel().DatabasePath);
    }
}
