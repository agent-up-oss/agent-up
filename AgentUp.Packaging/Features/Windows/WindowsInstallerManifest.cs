using AgentUp.Packaging.Features.ReleaseArtifacts;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AgentUp.Packaging.Features.Windows;

public sealed record WindowsInstallerManifest(
    string ProductName,
    string Manufacturer,
    string Version,
    string UpgradeCode,
    string ServiceName,
    string CliShimName,
    string BundleName)
{
    public static WindowsInstallerManifest From(PackageRequest request)
        => new(
            ProductName: "Agent-Up",
            Manufacturer: "Agent-Up",
            Version: request.NormalizedVersion,
            UpgradeCode: "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0",
            ServiceName: "agent-up-server",
            CliShimName: "agent-up.cmd",
            BundleName: "Agent-Up Setup");
}

public sealed class WindowsWixSourceGenerator
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";
    private static readonly XNamespace Bal = "http://wixtoolset.org/schemas/v4/wxs/bal";
    private readonly WindowsInstallerManifest _manifest;

    public WindowsWixSourceGenerator(WindowsInstallerManifest manifest)
    {
        _manifest = manifest;
    }

    public string ProductWxs(WindowsPackageLayout layout)
    {
        var package = new XElement(Wix + "Package",
            new XAttribute("Name", _manifest.ProductName),
            new XAttribute("Manufacturer", _manifest.Manufacturer),
            new XAttribute("Version", _manifest.Version),
            new XAttribute("UpgradeCode", _manifest.UpgradeCode),
            new XAttribute("Scope", "perMachine"));

        package.Add(new XElement(Wix + "MajorUpgrade",
            new XAttribute("DowngradeErrorMessage", "A newer version of Agent-Up is already installed.")));
        package.Add(new XElement(Wix + "MediaTemplate"));

        package.Add(new XElement(Wix + "StandardDirectory",
            new XAttribute("Id", "ProgramFiles64Folder"),
            new XElement(Wix + "Directory",
                new XAttribute("Id", "INSTALLFOLDER"),
                new XAttribute("Name", "Agent-Up"),
                new XElement(Wix + "Directory", new XAttribute("Id", "DesktopDir"), new XAttribute("Name", "desktop")),
                new XElement(Wix + "Directory", new XAttribute("Id", "ServerDir"), new XAttribute("Name", "server")),
                new XElement(Wix + "Directory", new XAttribute("Id", "CliDir"), new XAttribute("Name", "cli")),
                new XElement(Wix + "Directory", new XAttribute("Id", "BinDir"), new XAttribute("Name", "bin")))));

        package.Add(new XElement(Wix + "StandardDirectory",
            new XAttribute("Id", "ProgramMenuFolder"),
            new XElement(Wix + "Directory",
                new XAttribute("Id", "ApplicationProgramsFolder"),
                new XAttribute("Name", "Agent-Up"))));

        var feature = new XElement(Wix + "Feature",
            new XAttribute("Id", "MainFeature"),
            new XAttribute("Title", "Agent-Up"),
            new XAttribute("Level", "1"));
        package.Add(feature);

        foreach (var component in Components(layout))
        {
            package.Add(component.Element);
            feature.Add(new XElement(Wix + "ComponentRef", new XAttribute("Id", component.Id)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Wix + "Wix", package)).ToString();
    }

    public string BundleWxs(WindowsPackageLayout layout)
    {
        var bundle = new XElement(Wix + "Bundle",
            new XAttribute("Name", _manifest.BundleName),
            new XAttribute("Manufacturer", _manifest.Manufacturer),
            new XAttribute("Version", _manifest.Version),
            new XAttribute("UpgradeCode", StableGuid("bundle-upgrade")),
            new XAttribute(XNamespace.Xmlns + "bal", Bal),
            new XElement(Wix + "BootstrapperApplication",
                new XAttribute("Id", "WixStandardBootstrapperApplication.RtfLicense"),
                new XElement(Bal + "WixStandardBootstrapperApplication",
                    new XAttribute("LicenseFile", layout.LicenseRtfPath))),
            new XElement(Wix + "Chain",
                new XElement(Wix + "MsiPackage",
                    new XAttribute("SourceFile", layout.ProductMsiPath),
                    new XAttribute("DisplayInternalUI", "yes"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Wix + "Wix", bundle)).ToString();
    }

    public static string LicenseRtf()
        => @"{\rtf1\ansi Agent-Up installer. See the repository LICENSE file for license terms.}" + Environment.NewLine;

    private IEnumerable<(string Id, XElement Element)> Components(WindowsPackageLayout layout)
    {
        foreach (var file in Directory.EnumerateFiles(layout.DesktopPublishDirectory, "*", SearchOption.AllDirectories))
            yield return FileComponent("Desktop", "DesktopDir", layout.DesktopPublishDirectory, file);

        foreach (var file in Directory.EnumerateFiles(layout.ServerPublishDirectory, "*", SearchOption.AllDirectories))
        {
            var component = FileComponent("Server", "ServerDir", layout.ServerPublishDirectory, file);
            if (Path.GetFileName(file).Equals("AgentUp.Server.exe", StringComparison.OrdinalIgnoreCase))
            {
                component.Element.Add(
                    new XElement(Wix + "ServiceInstall",
                        new XAttribute("Id", "AgentUpServerService"),
                        new XAttribute("Type", "ownProcess"),
                        new XAttribute("Name", _manifest.ServiceName),
                        new XAttribute("DisplayName", "Agent-Up Server"),
                        new XAttribute("Description", "Local Agent-Up runtime authority for workspaces, processes, ports, diagnostics, and automation."),
                        new XAttribute("Start", "auto"),
                        new XAttribute("ErrorControl", "normal"),
                        new XAttribute("Arguments", "--urls http://127.0.0.1:5000")),
                    new XElement(Wix + "ServiceControl",
                        new XAttribute("Id", "StartAgentUpServerService"),
                        new XAttribute("Name", _manifest.ServiceName),
                        new XAttribute("Start", "install"),
                        new XAttribute("Stop", "both"),
                        new XAttribute("Remove", "uninstall"),
                        new XAttribute("Wait", "yes")));
            }

            yield return component;
        }

        foreach (var file in Directory.EnumerateFiles(layout.CliPublishDirectory, "*", SearchOption.AllDirectories))
            yield return FileComponent("Cli", "CliDir", layout.CliPublishDirectory, file);

        var cliShim = new XElement(Wix + "Component",
            new XAttribute("Id", "CliShimComponent"),
            new XAttribute("Directory", "BinDir"),
            new XAttribute("Guid", StableGuid("cli-shim")),
            new XElement(Wix + "File",
                new XAttribute("Id", "AgentUpCliShim"),
                new XAttribute("Source", Path.Combine(layout.InstallerSourceDirectory, _manifest.CliShimName)),
                new XAttribute("KeyPath", "yes")),
            new XElement(Wix + "Environment",
                new XAttribute("Id", "AgentUpPathEntry"),
                new XAttribute("Name", "PATH"),
                new XAttribute("Value", "[BinDir]"),
                new XAttribute("Permanent", "no"),
                new XAttribute("Part", "last"),
                new XAttribute("Action", "set"),
                new XAttribute("System", "yes")));
        yield return ("CliShimComponent", cliShim);

        var shortcut = new XElement(Wix + "Component",
            new XAttribute("Id", "StartMenuShortcutComponent"),
            new XAttribute("Directory", "ApplicationProgramsFolder"),
            new XAttribute("Guid", StableGuid("start-menu-shortcut")),
            new XElement(Wix + "Shortcut",
                new XAttribute("Id", "AgentUpStartMenuShortcut"),
                new XAttribute("Name", "Agent-Up"),
                new XAttribute("Target", "[DesktopDir]AgentUp.Desktop.exe"),
                new XAttribute("WorkingDirectory", "DesktopDir")),
            new XElement(Wix + "RemoveFolder",
                new XAttribute("Id", "RemoveApplicationProgramsFolder"),
                new XAttribute("On", "uninstall")),
            new XElement(Wix + "RegistryValue",
                new XAttribute("Root", "HKCU"),
                new XAttribute("Key", "Software\\Agent-Up"),
                new XAttribute("Name", "installed"),
                new XAttribute("Type", "integer"),
                new XAttribute("Value", "1"),
                new XAttribute("KeyPath", "yes")));
        yield return ("StartMenuShortcutComponent", shortcut);
    }

    private static (string Id, XElement Element) FileComponent(string prefix, string directoryId, string root, string file)
    {
        var relative = Path.GetRelativePath(root, file);
        var id = $"{prefix}_{Sanitize(relative)}";
        return (id, new XElement(Wix + "Component",
            new XAttribute("Id", id),
            new XAttribute("Directory", directoryId),
            new XAttribute("Guid", StableGuid(relative)),
            new XElement(Wix + "File",
                new XAttribute("Id", $"{id}_File"),
                new XAttribute("Source", file),
                new XAttribute("KeyPath", "yes"))));
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value)
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return builder.ToString();
    }

    private static string StableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("Agent-Up Windows Installer:" + value));
        return new Guid(bytes).ToString("D").ToUpperInvariant();
    }
}
