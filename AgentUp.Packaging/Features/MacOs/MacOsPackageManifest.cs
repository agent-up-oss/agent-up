using AgentUp.Packaging.Features.ReleaseArtifacts;
using System.Xml.Linq;

namespace AgentUp.Packaging.Features.MacOs;

public sealed record MacOsPackageManifest(
    string ProductName,
    string DesktopBundleIdentifier,
    string InstallerBundleIdentifier,
    string ServerLaunchDaemonLabel,
    string Version,
    string ServerUrl)
{
    public static MacOsPackageManifest From(PackageRequest request)
        => new(
            ProductName: "Agent-Up",
            DesktopBundleIdentifier: "dev.agent-up.desktop",
            InstallerBundleIdentifier: "dev.agent-up.installer",
            ServerLaunchDaemonLabel: "dev.agent-up.server",
            Version: request.NormalizedVersion,
            ServerUrl: "http://127.0.0.1:5000");
}

public sealed class MacOsPlistGenerator
{
    private readonly MacOsPackageManifest _manifest;

    public MacOsPlistGenerator(MacOsPackageManifest manifest)
    {
        _manifest = manifest;
    }

    public string DesktopInfoPlist()
        => Plist(Dict(
            KeyString("CFBundleName", _manifest.ProductName),
            KeyString("CFBundleDisplayName", _manifest.ProductName),
            KeyString("CFBundleIdentifier", _manifest.DesktopBundleIdentifier),
            KeyString("CFBundleExecutable", "AgentUp.Desktop"),
            KeyString("CFBundleVersion", _manifest.Version),
            KeyString("CFBundleShortVersionString", _manifest.Version),
            KeyString("CFBundlePackageType", "APPL")));

    public string InstallerInfoPlist()
        => Plist(Dict(
            KeyString("CFBundleIdentifier", _manifest.InstallerBundleIdentifier),
            KeyString("CFBundleName", "Agent-Up Installer"),
            KeyString("CFBundleDisplayName", "Agent-Up Installer"),
            KeyString("CFBundleExecutable", "AgentUpInstaller"),
            KeyString("CFBundleVersion", _manifest.Version),
            KeyString("CFBundleShortVersionString", _manifest.Version),
            KeyString("CFBundlePackageType", "APPL")));

    public string LaunchDaemonPlist()
        => Plist(Dict(
            KeyString("Label", _manifest.ServerLaunchDaemonLabel),
            new XElement("key", "ProgramArguments"),
            new XElement("array",
                new XElement("string", "/Library/Application Support/Agent-Up/server/AgentUp.Server"),
                new XElement("string", "--urls"),
                new XElement("string", _manifest.ServerUrl)),
            new XElement("key", "EnvironmentVariables"),
            new XElement("dict",
                KeyString("ASPNETCORE_URLS", _manifest.ServerUrl),
                KeyString("Storage__DataDirectory", "/Library/Application Support/Agent-Up")),
            new XElement("key", "RunAtLoad"),
            new XElement("true"),
            new XElement("key", "KeepAlive"),
            new XElement("true"),
            new XElement("key", "ThrottleInterval"),
            new XElement("integer", "5"),
            KeyString("StandardOutPath", "/Library/Logs/Agent-Up/server.out.log"),
            KeyString("StandardErrorPath", "/Library/Logs/Agent-Up/server.err.log")));

    private static string Plist(XElement dict)
        => new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "https://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"), dict)).ToString() + Environment.NewLine;

    private static XElement Dict(params object[] children)
        => new("dict", children);

    private static object[] KeyString(string key, string value)
        => [new XElement("key", key), new XElement("string", value)];
}
