using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EthangRss.Core;
using EthangRss.Core.Data;
using EthangRss.Desktop.Services;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;

namespace EthangRss.Desktop;

public partial class App : Application
{
    private TrayIcon? _tray;

    internal static ReaderService? Services { get; private set; }

    internal static Notifier? Notifier { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var database = new AppDatabase();
            database.Migrate();

            var settings = new SettingsStore(database);

            var service = new ReaderService(database, settings);
            Services = service;

            Notifier = new Notifier(
                () => settings.GetBool(SettingKeys.NotificationsEnabled, false),
                () => settings.GetInt(SettingKeys.NotificationThreshold, 1));

            ShowOnboardingIfNeeded(database, settings);

            var viewModel = new MainViewModel(service);
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;

            BuildTray(desktop, window, service, viewModel);

            window.Closing += (_, closing) =>
            {
                if (_tray is not null && !service.IsQuitting)
                {
                    closing.Cancel = true;
                    window.Hide();
                }
            };

            _ = service.StartPollingAsync(viewModel, Notifier, CancellationToken.None);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowOnboardingIfNeeded(AppDatabase database, SettingsStore settings)
    {
        var model = new OnboardingViewModel(settings);
        if (model.IsComplete)
        {
            return;
        }

        // Shown before the reader exists, so it is a standalone dialog rather than modal to it.
        new OnboardingWindow(model).Show();
    }

    private void BuildTray(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window window,
        ReaderService service,
        MainViewModel viewModel)
    {
        try
        {
            var menu = new NativeMenu();

            var open = new NativeMenuItem("Open");
            open.Click += (_, _) =>
            {
                window.Show();
                window.Activate();
            };
            menu.Add(open);

            var refresh = new NativeMenuItem("Refresh now");
            refresh.Click += (_, _) => _ = viewModel.RefreshAsync(CancellationToken.None);
            menu.Add(refresh);

            menu.Add(new NativeMenuItemSeparator());

            var quit = new NativeMenuItem("Quit");
            quit.Click += (_, _) =>
            {
                service.IsQuitting = true;
                desktop.Shutdown();
            };
            menu.Add(quit);

            _tray = new TrayIcon
            {
                Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")),
                ToolTipText = "Ethang RSS",
                Menu = menu,
                IsVisible = true,
            };

            _tray.Clicked += (_, _) =>
            {
                window.Show();
                window.Activate();
            };

            Notifier?.Attach((title, message) => _tray?.ToolTipText = title + ": " + message);
        }
        catch (Exception)
        {
            _tray = null;
        }
    }
}