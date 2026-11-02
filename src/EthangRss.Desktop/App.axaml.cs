using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EthangRss.Core.Data;
using EthangRss.Desktop.Services;
using EthangRss.Desktop.ViewModels;
using EthangRss.Desktop.Views;

namespace EthangRss.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var database = new AppDatabase();
            database.Migrate();

            var service = new ReaderService(database);
            var viewModel = new MainViewModel(service);

            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // Poll in the background while the window is open.
            _ = service.StartPollingAsync(viewModel, CancellationToken.None);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
