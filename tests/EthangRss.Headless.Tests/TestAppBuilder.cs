using Avalonia;
using Avalonia.Headless;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(EthangRss.Headless.Tests.TestAppBuilder))]

namespace EthangRss.Headless.Tests;

/// <summary>Builds the real application headlessly, so controls that need an Avalonia app can be
/// constructed in a test instead of only on a desktop.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<EthangRss.Desktop.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
