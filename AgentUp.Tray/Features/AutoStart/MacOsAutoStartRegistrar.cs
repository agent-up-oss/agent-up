using System.Xml.Linq;

namespace AgentUp.Tray.Features.AutoStart;

public sealed class MacOsAutoStartRegistrar : IAutoStartRegistrar
{
    private const string Label = "dev.agent-up.tray";

    private readonly string _plistPath;
    private readonly string _trayBinary;

    private readonly Action<string, string> _launchctl;

    public MacOsAutoStartRegistrar(string trayBinary)
        : this(trayBinary, DefaultLaunchAgentsDirectory(), LaunchctlProcess.Run)
    {
    }

    /// <summary>
    /// Takes the LaunchAgents directory and the launchctl invocation explicitly, so the
    /// plist contract and the load/unload sequence can be verified on any host.
    /// </summary>
    public MacOsAutoStartRegistrar(
        string trayBinary,
        string launchAgentsDirectory,
        Action<string, string> launchctl)
    {
        _trayBinary = trayBinary;
        _plistPath = Path.Join(launchAgentsDirectory, $"{Label}.plist");
        _launchctl = launchctl;
    }

    public static string DefaultLaunchAgentsDirectory()
        => Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");

    /// <summary>The launchd property list this registrar writes.</summary>
    public string PropertyList => GeneratePlist();

    public bool IsRegistered() => File.Exists(_plistPath);

    public void Register()
    {
        var dir = Path.GetDirectoryName(_plistPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_plistPath, GeneratePlist());

        _launchctl("load", _plistPath);
    }

    public void Unregister()
    {
        if (File.Exists(_plistPath))
        {
            _launchctl("unload", _plistPath);
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
