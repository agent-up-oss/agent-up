namespace AgentUp.Tray.Features.AutoStart;

public sealed class LinuxAutoStartRegistrar : IAutoStartRegistrar
{
    private const string FileName = "agent-up-tray.desktop";

    private readonly string _desktopFilePath;
    private readonly string _trayBinary;

    public LinuxAutoStartRegistrar(string trayBinary)
    {
        _trayBinary = trayBinary;
        var autostartDir = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart");
        _desktopFilePath = Path.Join(autostartDir, FileName);
    }

    public bool IsRegistered() => File.Exists(_desktopFilePath);

    public void Register()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_desktopFilePath)!);
        File.WriteAllText(_desktopFilePath, GenerateDesktopEntry());
    }

    public void Unregister()
    {
        if (File.Exists(_desktopFilePath))
            File.Delete(_desktopFilePath);
    }

    private string GenerateDesktopEntry() =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=Agent-Up Tray
        Comment=Agent-Up tray manager
        Exec={_trayBinary}
        Terminal=false
        Hidden=false
        X-GNOME-Autostart-enabled=true
        """ + Environment.NewLine;
}
