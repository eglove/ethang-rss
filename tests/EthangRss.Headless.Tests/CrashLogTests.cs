using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using EthangRss.Desktop.Services;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>
/// A fault on the UI thread must be written down and survived, not turned into a vanished window.
/// </summary>
public sealed class CrashLogTests
{
    [AvaloniaFact]
    public void An_exception_on_the_ui_thread_is_logged_and_does_not_escape()
    {
        var original = CrashLog.Path;
        var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ethangrss-crashlog-" + Guid.NewGuid().ToString("N") + ".log");
        CrashLog.Path = log;

        try
        {
            CrashLog.Install();
            Dispatcher.UIThread.Post(() => throw new InvalidOperationException("boom from a dispatcher job"));

            // Handled, so this returns rather than failing the test with the job's exception.
            Dispatcher.UIThread.RunJobs();

            Assert.True(File.Exists(log), "nothing was written to the crash log");
            var written = File.ReadAllText(log);
            Assert.Contains("boom from a dispatcher job", written, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", written, StringComparison.Ordinal);
        }
        finally
        {
            CrashLog.Path = original;
            if (File.Exists(log))
            {
                File.Delete(log);
            }
        }
    }
}