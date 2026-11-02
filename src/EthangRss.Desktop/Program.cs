using Avalonia;
using System;
using EthangRss.Desktop.Services;

namespace EthangRss.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // One poller per user session: a second launch would fight the first over the database.
        using var single = new SingleInstance();
        if (!single.TryClaim())
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
