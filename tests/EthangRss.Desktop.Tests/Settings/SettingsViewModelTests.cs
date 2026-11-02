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

    private void SeedArticle(string guid, DateTimeOffset published, bool starred = false)
    {
        var feed = _temp.InsertFeed("https://e.com/" + guid, "F" + guid);
        using var connection = _temp.Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into articles (FeedId, Guid, Title, Link, PublishedAt, FetchedAt, IsStarred) " +
            "values ($feed, $guid, $guid, 'https://e.com/a', $published, $published, $starred)";
        command.Parameters.AddWithValue("$feed", feed);
        command.Parameters.AddWithValue("$guid", guid);
        command.Parameters.AddWithValue("$published", published.ToString("O"));
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
        Assert.True(model.KeepArticlesForever);
        Assert.True(model.FilterYoutubeShorts);
        Assert.False(model.NotificationsEnabled);
        Assert.False(model.StartWithWindows);
    }

    [Fact]
    public void Saving_persists_every_value()
    {
        var model = NewModel();
        model.RefreshIntervalMinutes = 45;
        model.KeepArticlesForever = false;
        model.RetentionDays = 30;
        model.FilterYoutubeShorts = false;
        model.NotificationsEnabled = true;
        model.NotificationThreshold = 3;
        model.StartWithWindows = true;

        model.Save();

        var reloaded = NewModel();
        Assert.Equal(45, reloaded.RefreshIntervalMinutes);
        Assert.False(reloaded.KeepArticlesForever);
        Assert.Equal(30, reloaded.RetentionDays);
        Assert.False(reloaded.FilterYoutubeShorts);
        Assert.True(reloaded.NotificationsEnabled);
        Assert.Equal(3, reloaded.NotificationThreshold);
        Assert.True(reloaded.StartWithWindows);
    }

    [Fact]
    public void Retention_input_is_only_meaningful_when_keeping_forever_is_off()
    {
        var model = NewModel();

        Assert.False(model.RetentionEnabled);
        model.KeepArticlesForever = false;
        Assert.True(model.RetentionEnabled);
    }

    [Fact]
    public void Applying_retention_while_it_is_off_deletes_nothing()
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
        model.KeepArticlesForever = false;
        model.RetentionDays = 90;

        Assert.Equal(1, model.ApplyRetentionNow());
        Assert.Equal(2, CountArticles());
        Assert.Contains("Deleted 1 old article", model.StatusText);
    }

    [Fact]
    public void The_database_path_is_shown_so_the_folder_can_be_opened()
    {
        Assert.Equal(_temp.Database.DatabasePath, NewModel().DatabasePath);
    }
}
