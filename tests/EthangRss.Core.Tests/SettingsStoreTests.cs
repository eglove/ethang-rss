namespace EthangRss.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Missing_keys_return_the_fallback()
    {
        var store = new SettingsStore(_temp.Database);

        Assert.Equal(30, store.GetInt(SettingKeys.RefreshIntervalMinutes, 30));
        Assert.True(store.GetBool(SettingKeys.KeepArticlesForever, true));
    }

    [Fact]
    public void Values_round_trip_and_overwrite()
    {
        var store = new SettingsStore(_temp.Database);

        store.SetInt(SettingKeys.RefreshIntervalMinutes, 60);
        Assert.Equal(60, store.GetInt(SettingKeys.RefreshIntervalMinutes, 30));

        store.SetInt(SettingKeys.RefreshIntervalMinutes, 15);
        Assert.Equal(15, store.GetInt(SettingKeys.RefreshIntervalMinutes, 30));
    }

    [Fact]
    public void Booleans_round_trip()
    {
        var store = new SettingsStore(_temp.Database);

        store.SetBool(SettingKeys.FilterYoutubeShorts, false);
        Assert.False(store.GetBool(SettingKeys.FilterYoutubeShorts, true));
    }
}
