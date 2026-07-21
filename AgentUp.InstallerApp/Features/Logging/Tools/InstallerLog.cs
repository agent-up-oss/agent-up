namespace AgentUp.InstallerApp.Features.Logging.Tools;

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
                // chmod is best-effort: the log entry is already written; a permission failure
                // only affects future appends from a different user (e.g. GUI after root install).
                if (dirCreated)
                    try { File.SetUnixFileMode(dir, AllReadWrite | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute); }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                if (fileCreated)
                    try { File.SetUnixFileMode(path, AllReadWrite); }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
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
            return ResolveMacOsLogPath();

        if (OperatingSystem.IsWindows())
            return Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Agent-Up", "Logs", "installer.log");

        return Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "agent-up", "installer.log");
    }

    internal static string ResolveMacOsLogPath(bool? isPrivileged = null, bool? systemDirExists = null)
    {
        // Prefer the system path so the root PKG postinstall process (--install-core)
        // and the user GUI process both write to the same file.
        // Use it only when accessible: we're root, or a prior root run already created the directory.
        // Falls back to ~/Library/Logs when neither condition holds (fresh launch, CI, etc.).
        const string SystemLogPath = "/Library/Logs/Agent-Up/installer.log";
        if ((isPrivileged ?? Environment.IsPrivilegedProcess) || (systemDirExists ?? Directory.Exists("/Library/Logs/Agent-Up")))
            return SystemLogPath;

        return Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Logs", "Agent-Up", "installer.log");
    }
}
