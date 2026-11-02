using CommunityToolkit.Mvvm.ComponentModel;
using EthangRss.Core;
using EthangRss.Core.Import;

namespace EthangRss.Desktop.ViewModels;

/// <summary>
/// Shown once, on a brand-new database. The two honest choices are importing the subscriptions you
/// already have, or starting empty; nothing is fetched or guessed behind your back.
/// </summary>
public sealed partial class OnboardingViewModel : ViewModelBase
{
    private readonly SettingsStore _settings;
    private readonly Func<string, ImportResult> _import;

    public OnboardingViewModel(SettingsStore settings, Func<string, ImportResult> import)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(import);
        _settings = settings;
        _import = import;
        DumpPath = Path.Combine(Environment.CurrentDirectory, "d1-export.sql");
    }

    public static string WranglerCommand { get; } =
        "npx --yes wrangler@4 --cwd <path-to>/apps/ethang-rss d1 export ethang-rss --remote --no-schema --output d1-export.sql";

    [ObservableProperty]
    public partial string DumpPath { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int RefreshIntervalMinutes { get; set; } = 30;

    public bool IsComplete => _settings.GetBool(SettingKeys.OnboardingComplete, false);

    /// <summary>Runs the import. Returns false with a message when the dump is missing or unreadable.</summary>
    public bool Import()
    {
        if (string.IsNullOrWhiteSpace(DumpPath) || !File.Exists(DumpPath))
        {
            StatusText = "That file was not found. Export the database first, then point at the .sql file.";
            return false;
        }

        try
        {
            var result = _import(DumpPath);
            StatusText = "Imported " + result.FeedsImported + " feeds.";
            Finish();
            return result.Kept > 0;
        }
        catch (Exception exception)
        {
            StatusText = "The import failed: " + exception.Message;
            return false;
        }
    }

    /// <summary>Skip the import and start with an empty library.</summary>
    public void StartEmpty()
    {
        StatusText = "Starting empty. Add feeds with the Add feed button.";
        Finish();
    }

    private void Finish()
    {
        _settings.SetInt(SettingKeys.RefreshIntervalMinutes, Math.Max(1, RefreshIntervalMinutes));
        _settings.SetBool(SettingKeys.OnboardingComplete, true);
    }
}
