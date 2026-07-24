using System.Xml.Linq;

namespace AgentUp.Tray.Features.AutoStart;

public sealed class MacOsAutoStartRegistrar : IAutoStartRegistrar
{
    private const string Label = "dev.agent-up.tray";

    private readonly string _plistPath;
    private readonly string _trayBinary;

    public MacOsAutoStartRegistrar(string trayBinary)
    {
        _trayBinary = trayBinary;
        var launchAgentsDir = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");
        _plistPath = Path.Join(launchAgentsDir, $"{Label}.plist");
    }

    public bool IsRegistered() => File.Exists(_plistPath);

    public void Register()
    {
        var dir = Path.GetDirectoryName(_plistPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_plistPath, GeneratePlist());

        // Load into launchd for the current session (ignore errors if launchctl unavailable)
        try { System.Diagnostics.Process.Start("launchctl", ["load", _plistPath])?.WaitForExit(3000); }
        catch { /* launchctl not available or already loaded */ }
    }

    public void Unregister()
    {
        if (File.Exists(_plistPath))
        {
            try { System.Diagnostics.Process.Start("launchctl", ["unload", _plistPath])?.WaitForExit(3000); }
            catch { /* ignore */ }
            File.Delete(_plistPath);
        }
    }

    private string GeneratePlist()
        => new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN",
                "https://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "Label"), new XElement("string", Label),
                    new XElement("key", "ProgramArguments"),
                    new XElement("array", new XElement("string", _trayBinary)),
                    new XElement("key", "RunAtLoad"), new XElement("true"),
                    new XElement("key", "KeepAlive"), new XElement("true"),
                    new XElement("key", "ThrottleInterval"), new XElement("integer", "5"))))
        + Environment.NewLine;
}
