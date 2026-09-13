namespace AgentUp.Tray.Features.AutoStart;

public sealed class LinuxAutoStartRegistrar : IAutoStartRegistrar
{
    private const string FileName = "agent-up-tray.desktop";

    private readonly string _desktopFilePath;
    private readonly string _trayBinary;

    public LinuxAutoStartRegistrar(string trayBinary)
        : this(trayBinary, DefaultAutostartDirectory())
    {
    }

    /// <summary>
    /// Takes the autostart directory explicitly so the XDG desktop-entry contract can be
    /// verified on any host, rather than only on a Linux machine with a real home.
    /// </summary>
    public LinuxAutoStartRegistrar(string trayBinary, string autostartDirectory)
    {
        _trayBinary = trayBinary;
        _desktopFilePath = Path.Join(autostartDirectory, FileName);
    }

    public static string DefaultAutostartDirectory()
        => Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart");

    /// <summary>The desktop entry this registrar writes.</summary>
    public string DesktopEntry => GenerateDesktopEntry();

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
        Name=Agent-Up
        Comment=Agent-Up tray manager
        Exec={_trayBinary}
        Icon=agent-up
        Terminal=false
        Hidden=false
        X-GNOME-Autostart-enabled=true
        """ + Environment.NewLine;
}
