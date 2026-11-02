using CommunityToolkit.Mvvm.ComponentModel;
using EthangRss.Core;

namespace EthangRss.Desktop.ViewModels;

/// <summary>
/// Shown once, on a brand-new database. It asks for the one thing the reader cannot guess: how often
/// to check feeds. Nothing is fetched until you add a feed.
/// </summary>
public sealed partial class OnboardingViewModel : ViewModelBase
{
    private readonly SettingsStore _settings;

    public OnboardingViewModel(SettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    [ObservableProperty]
    public partial int RefreshIntervalMinutes { get; set; } = 30;

    public bool IsComplete => _settings.GetBool(SettingKeys.OnboardingComplete, false);

    /// <summary>Records the interval and marks the first run as done.</summary>
    public void Finish()
    {
        _settings.SetInt(SettingKeys.RefreshIntervalMinutes, Math.Max(1, RefreshIntervalMinutes));
        _settings.SetBool(SettingKeys.OnboardingComplete, true);
    }
}
