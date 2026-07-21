namespace AgentUp.InstallerApp.Features.Logging.Tools;

public static class InstallerLog
{
    private static readonly string LogPath = ResolveLogPath();

    public static string FilePath => LogPath;

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never crash the installer.
        }
    }

    public static void WriteException(string context, Exception exception)
        => Write($"ERROR in {context}: {exception}");

    private static string ResolveLogPath()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", "Agent-Up", "installer.log");

        if (OperatingSystem.IsWindows())
            return Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Agent-Up", "Logs", "installer.log");

        return Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "agent-up", "installer.log");
    }
}
