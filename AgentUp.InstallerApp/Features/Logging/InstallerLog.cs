namespace AgentUp.InstallerApp.Features.Logging;

public static class InstallerLog
{
    private static readonly string LogPath = ResolveLogPath();

    public static string FilePath => LogPath;

    public static void Write(string message) => WriteToPath(LogPath, message);

    public static void WriteToPath(string path, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(path)!;
            var dirCreated = !Directory.Exists(dir);
            Directory.CreateDirectory(dir);
            var fileCreated = !File.Exists(path);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}");
            // Make the log dir and file writable by all users so the GUI (running as the
            // logged-in user) can append after --install-core created them as root.
            if ((dirCreated || fileCreated) && !OperatingSystem.IsWindows())
            {
                const UnixFileMode AllReadWrite =
                    UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
                try { if (dirCreated) File.SetUnixFileMode(dir, AllReadWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute); } catch { }
                try { if (fileCreated) File.SetUnixFileMode(path, AllReadWrite); } catch { }
            }
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
            // Use a fixed system path so the root process (--install-core via PKG postinstall)
            // and the user process (GUI launched by open -a) both write to the same file.
            // ~/Library/Logs is user-owned, so a root-created file there blocks user appends.
            return "/Library/Logs/Agent-Up/installer.log";

        if (OperatingSystem.IsWindows())
            return Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Agent-Up", "Logs", "installer.log");

        return Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "agent-up", "installer.log");
    }
}
