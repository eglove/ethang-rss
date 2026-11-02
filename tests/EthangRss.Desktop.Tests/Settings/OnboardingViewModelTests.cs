using EthangRss.Core;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Tests.Settings;

public sealed class OnboardingViewModelTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly SettingsStore _store;

    public OnboardingViewModelTests() => _store = new SettingsStore(_temp.Database);

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void A_brand_new_database_is_not_onboarded()
    {
        Assert.False(new OnboardingViewModel(_store).IsComplete);
    }

    [Fact]
    public void Finishing_records_the_interval_and_marks_onboarding_done()
    {
        var model = new OnboardingViewModel(_store) { RefreshIntervalMinutes = 45 };

        model.Finish();

        Assert.True(_store.GetBool(SettingKeys.OnboardingComplete, false));
        Assert.Equal(45, _store.GetInt(SettingKeys.RefreshIntervalMinutes, 30));
    }

    [Fact]
    public void A_nonsense_interval_is_clamped_to_at_least_one_minute()
    {
        new OnboardingViewModel(_store) { RefreshIntervalMinutes = 0 }.Finish();

        Assert.Equal(1, _store.GetInt(SettingKeys.RefreshIntervalMinutes, 30));
    }

    [Fact]
    public void Onboarding_does_not_reappear_once_complete()
    {
        _store.SetBool(SettingKeys.OnboardingComplete, true);

        Assert.True(new OnboardingViewModel(_store).IsComplete);
    }
}
