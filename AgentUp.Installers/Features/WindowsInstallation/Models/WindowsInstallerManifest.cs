using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AgentUp.Installers.Features.WindowsInstallation.Models;

public sealed record WindowsInstallerManifest(
    string ProductName,
    string Manufacturer,
    string Version,
    string UpgradeCode,
    string ServiceName,
    string CliShimName,
    string BundleName,
    string ServerUrl)
{
    public const string DefaultCliShimName = "agent-up.cmd";

    public static WindowsInstallerManifest Create(string version)
        => new(
            ProductName: "Agent-Up",
            Manufacturer: "Agent-Up",
            Version: version,
            UpgradeCode: "5E8FB224-E5E3-4D48-8B62-2F50D521CBB0",
            ServiceName: "agent-up-server",
            CliShimName: DefaultCliShimName,
            BundleName: "Agent-Up",
            ServerUrl: "http://127.0.0.1:5000");
}

public sealed record WindowsInstallerLayout(
    string InstallerSourceDirectory,
    string InstallerPublishDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory,
    string LicenseRtfPath,
    string ProductMsiPath);

public sealed class WindowsWixSourceGenerator
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";
    private static readonly XNamespace Bal = "http://wixtoolset.org/schemas/v4/wxs/bal";
    private readonly WindowsInstallerManifest _manifest;

    public WindowsWixSourceGenerator(WindowsInstallerManifest manifest)
    {
        _manifest = manifest;
    }

    public string ProductWxs(WindowsInstallerLayout layout)
    {
        var package = new XElement(Wix + "Package",
            new XAttribute("Name", _manifest.ProductName),
            new XAttribute("Manufacturer", _manifest.Manufacturer),
            new XAttribute("Version", _manifest.Version),
            new XAttribute("UpgradeCode", _manifest.UpgradeCode),
            new XAttribute("Scope", "perMachine"));

        package.Add(new XElement(Wix + "MajorUpgrade",
            new XAttribute("DowngradeErrorMessage", "A newer version of Agent-Up is already installed.")));
        package.Add(new XElement(Wix + "MediaTemplate", new XAttribute("EmbedCab", "yes")));
        package.Add(
            new XElement(Wix + "Property", new XAttribute("Id", "ARPNOMODIFY"), new XAttribute("Value", "1")),
            new XElement(Wix + "Property", new XAttribute("Id", "ARPNOREPAIR"), new XAttribute("Value", "1")));

        package.Add(new XElement(Wix + "StandardDirectory",
            new XAttribute("Id", "ProgramFiles64Folder"),
            new XElement(Wix + "Directory",
                new XAttribute("Id", "INSTALLFOLDER"),
                new XAttribute("Name", _manifest.ProductName),
                new XElement(Wix + "Directory", new XAttribute("Id", "DesktopDir"), new XAttribute("Name", "desktop")),
                new XElement(Wix + "Directory", new XAttribute("Id", "ServerDir"), new XAttribute("Name", "server")),
                new XElement(Wix + "Directory", new XAttribute("Id", "CliDir"), new XAttribute("Name", "cli")),
                new XElement(Wix + "Directory",
                    new XAttribute("Id", "InstallerDir"),
                    new XAttribute("Name", "installer"),
                    new XElement(Wix + "Directory",
                        new XAttribute("Id", "InstallerPayloadDir"),
                        new XAttribute("Name", "payload"),
                        new XElement(Wix + "Directory", new XAttribute("Id", "InstallerPayloadDesktopDir"), new XAttribute("Name", "desktop")),
                        new XElement(Wix + "Directory", new XAttribute("Id", "InstallerPayloadServerDir"), new XAttribute("Name", "server")),
                        new XElement(Wix + "Directory", new XAttribute("Id", "InstallerPayloadCliDir"), new XAttribute("Name", "cli")))),
                new XElement(Wix + "Directory", new XAttribute("Id", "BinDir"), new XAttribute("Name", "bin")))));

        package.Add(new XElement(Wix + "StandardDirectory",
            new XAttribute("Id", "ProgramMenuFolder"),
            new XElement(Wix + "Directory",
                new XAttribute("Id", "ApplicationProgramsFolder"),
                new XAttribute("Name", _manifest.ProductName))));

        var feature = new XElement(Wix + "Feature",
            new XAttribute("Id", "MainFeature"),
            new XAttribute("Title", _manifest.ProductName),
            new XAttribute("Level", "1"));
        package.Add(feature);

        foreach (var component in Components(layout))
        {
            package.Add(component.Element);
            feature.Add(new XElement(Wix + "ComponentRef", new XAttribute("Id", component.Id)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Wix + "Wix", package)).ToString();
    }

    public string BundleWxs(WindowsInstallerLayout layout)
    {
        var bundle = new XElement(Wix + "Bundle",
            new XAttribute("Name", _manifest.BundleName),
            new XAttribute("Manufacturer", _manifest.Manufacturer),
            new XAttribute("Version", _manifest.Version),
            new XAttribute("UpgradeCode", StableGuid("bundle-upgrade")),
            new XAttribute(XNamespace.Xmlns + "bal", Bal),
            new XElement(Wix + "BootstrapperApplication",
                new XElement(Bal + "WixStandardBootstrapperApplication",
                    new XAttribute("Theme", "rtfLicense"),
                    new XAttribute("LicenseFile", layout.LicenseRtfPath),
                    new XAttribute("LaunchTarget", @"[ProgramFiles64Folder]Agent-Up\installer\AgentUp.InstallerApp.exe"),
                    new XAttribute("LaunchWorkingFolder", @"[ProgramFiles64Folder]Agent-Up\installer"))),
            new XElement(Wix + "Chain",
                new XElement(Wix + "MsiPackage",
                    new XAttribute("SourceFile", layout.ProductMsiPath),
                    new XAttribute("Vital", "yes"))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Wix + "Wix", bundle)).ToString();
    }

    public static string LicenseRtf()
        => @"{\rtf1\ansi Agent-Up installer. See the repository LICENSE file for license terms.}" + Environment.NewLine;

    public static string CliShimText()
        => "@echo off\r\n\"%~dp0..\\cli\\AgentUp.CLI.exe\" %*\r\n";

    private IEnumerable<(string Id, XElement Element)> Components(WindowsInstallerLayout layout)
    {
        foreach (var file in Directory.EnumerateFiles(layout.InstallerPublishDirectory, "*", SearchOption.AllDirectories))
            yield return FileComponent("Installer", "InstallerDir", layout.InstallerPublishDirectory, file);

        foreach (var file in Directory.EnumerateFiles(layout.DesktopPublishDirectory, "*", SearchOption.AllDirectories))
        {
            yield return FileComponent("Desktop", "DesktopDir", layout.DesktopPublishDirectory, file);
            yield return FileComponent("InstallerPayloadDesktop", "InstallerPayloadDesktopDir", layout.DesktopPublishDirectory, file);
        }

        foreach (var file in Directory.EnumerateFiles(layout.ServerPublishDirectory, "*", SearchOption.AllDirectories))
        {
            var component = FileComponent("Server", "ServerDir", layout.ServerPublishDirectory, file);
            if (System.IO.Path.GetFileName(file).Equals("AgentUp.Server.exe", StringComparison.OrdinalIgnoreCase))
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
                        new XAttribute("Arguments", $"--urls {_manifest.ServerUrl}")),
                    new XElement(Wix + "ServiceControl",
                        new XAttribute("Id", "StartAgentUpServerService"),
                        new XAttribute("Name", _manifest.ServiceName),
                        new XAttribute("Stop", "uninstall"),
                        new XAttribute("Remove", "uninstall"),
                        new XAttribute("Wait", "yes")));
            }

            yield return component;
            yield return FileComponent("InstallerPayloadServer", "InstallerPayloadServerDir", layout.ServerPublishDirectory, file);
        }

        foreach (var file in Directory.EnumerateFiles(layout.CliPublishDirectory, "*", SearchOption.AllDirectories))
        {
            yield return FileComponent("Cli", "CliDir", layout.CliPublishDirectory, file);
            yield return FileComponent("InstallerPayloadCli", "InstallerPayloadCliDir", layout.CliPublishDirectory, file);
        }

        var cliShim = new XElement(Wix + "Component",
            new XAttribute("Id", "CliShimComponent"),
            new XAttribute("Directory", "BinDir"),
            new XAttribute("Guid", StableGuid("cli-shim")),
            new XElement(Wix + "File",
                new XAttribute("Id", "AgentUpCliShim"),
                new XAttribute("Source", System.IO.Path.Join(layout.InstallerSourceDirectory, _manifest.CliShimName)),
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

        var appShortcut = new XElement(Wix + "Component",
            new XAttribute("Id", "StartMenuShortcutComponent"),
            new XAttribute("Directory", "ApplicationProgramsFolder"),
            new XAttribute("Guid", StableGuid("start-menu-shortcut")),
            new XElement(Wix + "Shortcut",
                new XAttribute("Id", "AgentUpStartMenuShortcut"),
                new XAttribute("Name", _manifest.ProductName),
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
        yield return ("StartMenuShortcutComponent", appShortcut);

        var installerShortcut = new XElement(Wix + "Component",
            new XAttribute("Id", "InstallerStartMenuShortcutComponent"),
            new XAttribute("Directory", "ApplicationProgramsFolder"),
            new XAttribute("Guid", StableGuid("installer-start-menu-shortcut")),
            new XElement(Wix + "Shortcut",
                new XAttribute("Id", "AgentUpInstallerStartMenuShortcut"),
                new XAttribute("Name", "Agent-Up Installer"),
                new XAttribute("Target", "[InstallerDir]AgentUp.InstallerApp.exe"),
                new XAttribute("WorkingDirectory", "InstallerDir")),
            new XElement(Wix + "RegistryValue",
                new XAttribute("Root", "HKCU"),
                new XAttribute("Key", "Software\\Agent-Up"),
                new XAttribute("Name", "installerInstalled"),
                new XAttribute("Type", "integer"),
                new XAttribute("Value", "1"),
                new XAttribute("KeyPath", "yes")));
        yield return ("InstallerStartMenuShortcutComponent", installerShortcut);
    }

    private static (string Id, XElement Element) FileComponent(string prefix, string directoryId, string root, string file)
    {
        var relative = System.IO.Path.GetRelativePath(root, file);
        var id = $"{prefix}_{Sanitize(relative)}";
        return (id, new XElement(Wix + "Component",
            new XAttribute("Id", id),
            new XAttribute("Directory", directoryId),
            new XAttribute("Guid", StableGuid($"{prefix}:{relative}")),
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
