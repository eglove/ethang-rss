using CommunityToolkit.Mvvm.ComponentModel;
using EthangRss.Core;
using EthangRss.Core.Retention;

namespace EthangRss.Desktop.ViewModels;

/// <summary>Every user-facing setting, with the defaults the spec fixed.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly Core.Data.AppDatabase _database;
    private readonly Func<DateTimeOffset> _clock;

    public SettingsViewModel(SettingsStore store, Core.Data.AppDatabase database, Func<DateTimeOffset> clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        _store = store;
        _database = database;
        _clock = clock;

        RefreshIntervalMinutes = store.GetInt(SettingKeys.RefreshIntervalMinutes, 30);
        KeepArticlesForever = store.GetBool(SettingKeys.KeepArticlesForever, true);
        RetentionDays = store.GetInt(SettingKeys.RetentionDays, 90);
        FilterYoutubeShorts = store.GetBool(SettingKeys.FilterYoutubeShorts, true);
        NotificationsEnabled = store.GetBool(SettingKeys.NotificationsEnabled, false);
        NotificationThreshold = store.GetInt(SettingKeys.NotificationThreshold, 1);
        StartWithWindows = store.GetBool(SettingKeys.StartWithWindows, false);
        DatabasePath = database.DatabasePath;
    }

    public static SettingsViewModel ForTests(SettingsStore store, Core.Data.AppDatabase database, Func<DateTimeOffset> clock) =>
        new(store, database, clock);

    public string DatabasePath { get; }

    [ObservableProperty]
    public partial int RefreshIntervalMinutes { get; set; }

    [ObservableProperty]
    public partial bool KeepArticlesForever { get; set; }

    [ObservableProperty]
    public partial int RetentionDays { get; set; }

    [ObservableProperty]
    public partial bool FilterYoutubeShorts { get; set; }

    [ObservableProperty]
    public partial bool NotificationsEnabled { get; set; }

    [ObservableProperty]
    public partial int NotificationThreshold { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public bool RetentionEnabled => !KeepArticlesForever;

    partial void OnKeepArticlesForeverChanged(bool value) => OnPropertyChanged(nameof(RetentionEnabled));

    public void Save()
    {
        _store.SetInt(SettingKeys.RefreshIntervalMinutes, Math.Max(1, RefreshIntervalMinutes));
        _store.SetBool(SettingKeys.KeepArticlesForever, KeepArticlesForever);
        _store.SetInt(SettingKeys.RetentionDays, Math.Max(1, RetentionDays));
        _store.SetBool(SettingKeys.FilterYoutubeShorts, FilterYoutubeShorts);
        _store.SetBool(SettingKeys.NotificationsEnabled, NotificationsEnabled);
        _store.SetInt(SettingKeys.NotificationThreshold, Math.Max(1, NotificationThreshold));
        _store.SetBool(SettingKeys.StartWithWindows, StartWithWindows);
        StatusText = "Settings saved";
    }

    /// <summary>Applies cleanup now. Returns the number of articles removed.</summary>
    public int ApplyRetentionNow()
    {
        if (KeepArticlesForever)
        {
            StatusText = "Retention is off, so nothing was deleted.";
            return 0;
        }

        var deleted = ArticleRetention.Apply(_database, Math.Max(1, RetentionDays), _clock());
        StatusText = deleted == 1 ? "Deleted 1 old article" : "Deleted " + deleted + " old articles";
        return deleted;
    }
}
