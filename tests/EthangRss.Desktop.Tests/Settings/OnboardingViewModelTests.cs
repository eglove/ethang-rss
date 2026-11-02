using EthangRss.Core;
using EthangRss.Core.Import;
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
        var model = new OnboardingViewModel(_store, _ => new ImportResult(0, 0, 0));

        Assert.False(model.IsComplete);
    }

    [Fact]
    public void Starting_empty_records_the_choice_and_the_interval()
    {
        var model = new OnboardingViewModel(_store, _ => new ImportResult(0, 0, 0)) { RefreshIntervalMinutes = 45 };

        model.StartEmpty();

        Assert.True(_store.GetBool(SettingKeys.OnboardingComplete, false));
        Assert.Equal(45, _store.GetInt(SettingKeys.RefreshIntervalMinutes, 30));
    }

    [Fact]
    public void Importing_reports_the_count_and_marks_onboarding_done()
    {
        var dump = Path.Combine(Path.GetTempPath(), "onboarding-" + Guid.NewGuid().ToString("N") + ".sql");
        File.WriteAllText(dump, "select 1;");

        try
        {
            var model = new OnboardingViewModel(_store, _ => new ImportResult(151, 0, 17)) { DumpPath = dump };

            Assert.True(model.Import());
            Assert.Contains("151", model.StatusText, StringComparison.Ordinal);
            Assert.True(_store.GetBool(SettingKeys.OnboardingComplete, false));
        }
        finally
        {
            File.Delete(dump);
        }
    }

    [Fact]
    public void A_missing_dump_is_reported_and_does_not_mark_onboarding_done()
    {
        var model = new OnboardingViewModel(_store, _ => new ImportResult(0, 0, 0))
        {
            DumpPath = Path.Combine(Path.GetTempPath(), "definitely-not-here-" + Guid.NewGuid().ToString("N") + ".sql"),
        };

        Assert.False(model.Import());
        Assert.Contains("not found", model.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.False(_store.GetBool(SettingKeys.OnboardingComplete, false));
    }

    [Fact]
    public void A_failing_import_is_caught_and_explained()
    {
        var dump = Path.Combine(Path.GetTempPath(), "onboarding-" + Guid.NewGuid().ToString("N") + ".sql");
        File.WriteAllText(dump, "select 1;");

        try
        {
            var model = new OnboardingViewModel(_store, _ => throw new InvalidOperationException("bad dump")) { DumpPath = dump };

            Assert.False(model.Import());
            Assert.Contains("bad dump", model.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dump);
        }
    }

    [Fact]
    public void Onboarding_does_not_reappear_once_complete()
    {
        _store.SetBool(SettingKeys.OnboardingComplete, true);

        Assert.True(new OnboardingViewModel(_store, _ => new ImportResult(0, 0, 0)).IsComplete);
    }
}
