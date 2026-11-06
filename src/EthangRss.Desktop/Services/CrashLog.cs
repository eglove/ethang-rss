using System.Text;
using Avalonia.Threading;

namespace EthangRss.Desktop.Services;

/// <summary>
/// Last-resort reporting. A reader should not disappear because one write failed or one control
/// could not start, so every unhandled exception is written down and the app keeps running. The
/// log is the only place a user can look afterwards, so it records the exception in full.
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();
    private static bool _installed;

    /// <summary>The log file. Settable so a test can capture entries without touching the real one.</summary>
    public static string Path { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EthangRss",
        "crash.log");

    /// <summary>Hooks the three places an unhandled exception can arrive. Safe to call twice.</summary>
    public static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            Record("ui", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Record("process", exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Record("task", args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>Appends one entry. Never throws: a failure to log must not become a second failure.</summary>
    public static void Record(string source, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var entry = new StringBuilder()
            .Append(DateTimeOffset.UtcNow.ToString("O"))
            .Append(" [").Append(source).Append("] ")
            .Append(exception.GetType().FullName)
            .Append(": ").AppendLine(exception.Message)
            .AppendLine(exception.ToString())
            .AppendLine()
            .ToString();

        try
        {
            lock (Gate)
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(Path, entry);
            }
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }
    }
}