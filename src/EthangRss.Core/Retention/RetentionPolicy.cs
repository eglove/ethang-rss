using EthangRss.Core.Data;

namespace EthangRss.Core.Retention;

/// <summary>
/// How long articles are kept, tracked separately for unread and read ones. A null window means
/// that state is kept forever, which is the default: deleting history is the one action that
/// cannot be undone.
/// </summary>
public sealed record RetentionPolicy(int? UnreadDays, int? ReadDays)
{
    public const int DefaultDays = 90;

    public static RetentionPolicy KeepEverything { get; } = new(null, null);

    public bool KeepsUnreadForever => UnreadDays is null;

    public bool KeepsReadForever => ReadDays is null;

    /// <summary>True when neither window is set, so a cleanup pass has nothing to do.</summary>
    public bool IsOff => UnreadDays is null && ReadDays is null;

    /// <summary>
    /// Reads the policy, falling back to the single window that existed before read and unread were
    /// split apart, so an existing choice carries over to both halves instead of resetting.
    /// </summary>
    public static RetentionPolicy Load(SettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var legacyKeepForever = store.GetBool(SettingKeys.KeepArticlesForever, true);
        var legacyDays = store.GetInt(SettingKeys.RetentionDays, DefaultDays);

        return new RetentionPolicy(
            store.GetBool(SettingKeys.KeepUnreadForever, legacyKeepForever)
                ? null
                : store.GetInt(SettingKeys.UnreadRetentionDays, legacyDays),
            store.GetBool(SettingKeys.KeepReadForever, legacyKeepForever)
                ? null
                : store.GetInt(SettingKeys.ReadRetentionDays, legacyDays));
    }

    public void Save(SettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        store.SetBool(SettingKeys.KeepUnreadForever, KeepsUnreadForever);
        store.SetInt(SettingKeys.UnreadRetentionDays, UnreadDays ?? DefaultDays);
        store.SetBool(SettingKeys.KeepReadForever, KeepsReadForever);
        store.SetInt(SettingKeys.ReadRetentionDays, ReadDays ?? DefaultDays);
    }
}
