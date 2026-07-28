using System.Text.RegularExpressions;
using System.Xml.Linq;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.MacOsInstallation.Models;

public sealed record MacOsInstallerManifest(
    string ProductName,
    string DesktopBundleIdentifier,
    string InstallerBundleIdentifier,
    string ServerLaunchDaemonLabel,
    string TrayLaunchAgentLabel,
    string BundleIconFile,
    string Version,
    string ServerUrl)
{
    public static MacOsInstallerManifest Create(string version)
        => new(
            ProductName: "Agent-Up",
            DesktopBundleIdentifier: "dev.agent-up.desktop",
            InstallerBundleIdentifier: "dev.agent-up.installer",
            ServerLaunchDaemonLabel: "dev.agent-up.server",
            TrayLaunchAgentLabel: "dev.agent-up.tray",
            BundleIconFile: "Agent-Up.png",
            Version: version,
            ServerUrl: "http://127.0.0.1:5000");

    private static readonly Regex SafeSlug = new(@"^[a-z][a-z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex SafeName = new(@"^[A-Za-z][A-Za-z0-9 -]*$", RegexOptions.Compiled);

    public static MacOsInstallerManifest From(
        ProductManifest product,
        string version,
        string serverUrl = "http://127.0.0.1:5000")
        => From(product.ProductName, product.Slug, version, serverUrl);

    public static MacOsInstallerManifest From(
        string productName,
        string slug,
        string version,
        string serverUrl = "http://127.0.0.1:5000")
    {
        var identity = ValidatedIdentityFrom(productName, slug);

        return new(
            ProductName: identity.ProductName,
            DesktopBundleIdentifier: $"dev.{identity.Slug}.desktop",
            InstallerBundleIdentifier: $"dev.{identity.Slug}.installer",
            ServerLaunchDaemonLabel: $"dev.{identity.Slug}.server",
            TrayLaunchAgentLabel: $"dev.{identity.Slug}.tray",
            BundleIconFile: identity.BundleIconFile,
            Version: version,
            ServerUrl: serverUrl);
    }

    internal static MacOsProductIdentity ValidatedIdentityFrom(ProductManifest product)
        => ValidatedIdentityFrom(product.ProductName, product.Slug);

    internal static MacOsProductIdentity ValidatedIdentityFrom(string productName, string slug)
    {
        if (!SafeSlug.IsMatch(slug))
            throw new ArgumentException(
                $"Slug '{slug}' must contain only lowercase letters, digits, and hyphens.",
                nameof(slug));
        if (!SafeName.IsMatch(productName))
            throw new ArgumentException(
                $"ProductName '{productName}' contains characters that are unsafe for macOS paths.",
                nameof(productName));

        return new(productName, slug, $"{productName.Replace(" ", "-")}.png");
    }
}

internal sealed record MacOsProductIdentity(string ProductName, string Slug, string BundleIconFile);

public sealed class MacOsInstallerPlistGenerator
{
    private readonly MacOsInstallerManifest _manifest;

    public MacOsInstallerPlistGenerator(MacOsInstallerManifest manifest)
    {
        _manifest = manifest;
    }

    public string DesktopInfoPlist()
        => Plist(Dict(
            KeyString("CFBundleName", _manifest.ProductName),
            KeyString("CFBundleDisplayName", _manifest.ProductName),
            KeyString("CFBundleIdentifier", _manifest.DesktopBundleIdentifier),
            KeyString("CFBundleExecutable", "AgentUp.Desktop"),
            KeyString("CFBundleIconFile", _manifest.BundleIconFile),
            KeyString("CFBundleVersion", _manifest.Version),
            KeyString("CFBundleShortVersionString", _manifest.Version),
            KeyString("CFBundlePackageType", "APPL")));

    public string InstallerInfoPlist()
        => Plist(Dict(
            KeyString("CFBundleIdentifier", _manifest.InstallerBundleIdentifier),
            KeyString("CFBundleName", $"{_manifest.ProductName} Installer"),
            KeyString("CFBundleDisplayName", $"{_manifest.ProductName} Installer"),
            KeyString("CFBundleExecutable", "AgentUp.InstallerApp"),
            KeyString("CFBundleIconFile", _manifest.BundleIconFile),
            KeyString("CFBundleVersion", _manifest.Version),
            KeyString("CFBundleShortVersionString", _manifest.Version),
            KeyString("CFBundlePackageType", "APPL")));

    public string TrayLaunchAgentPlist(string trayExecutable)
        => Plist(Dict(
            KeyString("Label", _manifest.TrayLaunchAgentLabel),
            new XElement("key", "ProgramArguments"),
            new XElement("array", new XElement("string", trayExecutable)),
            new XElement("key", "RunAtLoad"),
            new XElement("true"),
            new XElement("key", "KeepAlive"),
            new XElement("true"),
            new XElement("key", "ThrottleInterval"),
            new XElement("integer", "5")));

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
                KeyString("Storage__DataDirectory", "/Library/Application Support/Agent-Up"),
                KeyString("DOTNET_BUNDLE_EXTRACT_BASE_DIR", "/Library/Application Support/Agent-Up/bundle-cache")),
            new XElement("key", "RunAtLoad"),
            new XElement("true"),
            new XElement("key", "KeepAlive"),
            new XElement("dict",
                new XElement("key", "SuccessfulExit"),
                new XElement("false")),
            new XElement("key", "ThrottleInterval"),
            new XElement("integer", "5"),
            KeyString("StandardOutPath", "/Library/Logs/Agent-Up/server.out.log"),
            KeyString("StandardErrorPath", "/Library/Logs/Agent-Up/server.err.log")));

    private static string Plist(XElement dict)
        => new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "https://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"), dict)) + Environment.NewLine;

    private static XElement Dict(params object[] children)
        => new("dict", children);

    private static object[] KeyString(string key, string value)
        => [new XElement("key", key), new XElement("string", value)];
}
