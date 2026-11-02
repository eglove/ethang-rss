using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EthangRss.Core.Data;
using EthangRss.Core.Shell;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel? _model;

    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(SettingsViewModel model, AppDatabase database)
        : this()
    {
        _model = model;
        DataContext = model;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_model is null)
        {
            return;
        }

        _model.Save();

        if (OperatingSystem.IsWindows())
        {
            AutoStartRegistry.Apply(_model.StartWithWindows);
        }
    }

    private void OnCleanupClick(object? sender, RoutedEventArgs e) => _model?.ApplyRetentionNow();

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_model is null)
        {
            return;
        }

        var folder = Path.GetDirectoryName(_model.DatabasePath);
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening a folder is a convenience, not a requirement.
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

/// <summary>The one place that touches the Windows registry, kept out of Core and out of tests.</summary>
public static class AutoStartRegistry
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutoStart.RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var executable = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(executable))
                {
                    key.SetValue(AutoStart.ValueName, AutoStart.BuildCommand(executable));
                }
            }
            else
            {
                key.DeleteValue(AutoStart.ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // A locked-down machine must not break the app over a startup entry.
        }
    }
}
