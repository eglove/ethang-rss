using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using EthangRss.Core;
using EthangRss.Core.Data;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>Builds the real settings window, so a missing or misnamed binding fails here instead
/// of only when a person opens the dialog.</summary>
public sealed class SettingsWindowHeadlessTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "ethangrss-settings-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly AppDatabase _database;

    public SettingsWindowHeadlessTests()
    {
        _database = new AppDatabase(_path);
        _database.Migrate();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }
    }

    private SettingsWindow Show()
    {
        var store = new SettingsStore(_database);
        var model = new SettingsViewModel(store, _database, () => DateTimeOffset.UtcNow);
        var window = new SettingsWindow(model, _database);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static List<string?> CheckBoxLabels(Window window) =>
        window.GetVisualDescendants()
            .OfType<CheckBox>()
            .Select(box => box.Content as string)
            .ToList();

    [AvaloniaFact]
    public void The_history_section_offers_a_separate_switch_for_read_and_unread()
    {
        var window = Show();
        var labels = CheckBoxLabels(window);

        Assert.Contains("Keep unread articles forever", labels);
        Assert.Contains("Keep read articles forever", labels);
        Assert.DoesNotContain("Keep every article forever", labels);

        window.Close();
    }

    [AvaloniaFact]
    public void Each_history_switch_drives_its_own_day_input()
    {
        var window = Show();
        var model = (SettingsViewModel)window.DataContext!;

        var dayInputs = window.GetVisualDescendants().OfType<NumericUpDown>().ToList();

        // Refresh interval, unread window, read window, notification threshold.
        Assert.Equal(4, dayInputs.Count);

        // IsEnabled is the local value; the parent row greys the input out through inheritance.
        Assert.False(dayInputs[1].IsEffectivelyEnabled, "the unread window is editable while unread is kept forever");
        Assert.False(dayInputs[2].IsEffectivelyEnabled, "the read window is editable while read is kept forever");

        model.KeepUnreadForever = false;
        window.UpdateLayout();

        Assert.True(dayInputs[1].IsEffectivelyEnabled, "turning off keep-forever did not enable the unread window");
        Assert.False(dayInputs[2].IsEffectivelyEnabled, "the read window followed the unread switch");

        model.KeepReadForever = false;
        window.UpdateLayout();

        Assert.True(dayInputs[2].IsEffectivelyEnabled, "turning off keep-forever did not enable the read window");

        window.Close();
    }
}
