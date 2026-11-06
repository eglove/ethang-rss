using EthangRss.Core;
using EthangRss.Core.Retention;

namespace EthangRss.Core.Tests.Retention;

public sealed class RetentionPolicyTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly SettingsStore _store;

    public RetentionPolicyTests() => _store = new SettingsStore(_temp.Database);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_fresh_database_keeps_everything()
    {
        var policy = RetentionPolicy.Load(_store);

        Assert.True(policy.KeepsUnreadForever);
        Assert.True(policy.KeepsReadForever);
        Assert.True(policy.IsOff);
    }

    [Fact]
    public void The_two_windows_round_trip_independently()
    {
        new RetentionPolicy(UnreadDays: null, ReadDays: 14).Save(_store);

        var reloaded = RetentionPolicy.Load(_store);

        Assert.Null(reloaded.UnreadDays);
        Assert.Equal(14, reloaded.ReadDays);
        Assert.False(reloaded.IsOff);
    }

    [Fact]
    public void The_old_single_window_carries_over_to_both_halves()
    {
        // What an existing install has: one window, applied to everything, switched on.
        _store.SetBool(SettingKeys.KeepArticlesForever, false);
        _store.SetInt(SettingKeys.RetentionDays, 45);

        var policy = RetentionPolicy.Load(_store);

        Assert.Equal(45, policy.UnreadDays);
        Assert.Equal(45, policy.ReadDays);
    }

    [Fact]
    public void Saving_replaces_the_old_window_so_it_cannot_resurface()
    {
        _store.SetBool(SettingKeys.KeepArticlesForever, false);
        _store.SetInt(SettingKeys.RetentionDays, 45);

        new RetentionPolicy(UnreadDays: null, ReadDays: 7).Save(_store);

        var reloaded = RetentionPolicy.Load(_store);
        Assert.Null(reloaded.UnreadDays);
        Assert.Equal(7, reloaded.ReadDays);
    }
}
