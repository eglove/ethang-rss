namespace EthangRss.Core.Shell;

/// <summary>
/// Builds and removes the Run-key entry that starts the reader with Windows. The registry itself is
/// touched only by the app; this stays pure so the exact value can be asserted in a test.
/// </summary>
public static class AutoStart
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string ValueName = "EthangRss";

    public static string BuildCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return "\"" + executablePath.Trim('\"') + "\"";
    }
}
