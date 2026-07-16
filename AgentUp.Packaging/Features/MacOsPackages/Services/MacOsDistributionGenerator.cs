using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using System.Xml.Linq;
using AgentUp.Packaging.Features.MacOsPackages.Models;

namespace AgentUp.Packaging.Features.MacOsPackages.Services;

public static class MacOsDistributionGenerator
{
    public static string DistributionXml(MacOsPackageLayout layout, MacOsPackageManifest manifest)
        => new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("installer-gui-script",
                new XAttribute("minSpecVersion", "1"),
                new XElement("title", manifest.InstallerManifest.ProductName),
                new XElement("options",
                    new XAttribute("customize", "never"),
                    new XAttribute("require-scripts", "false")),
                PkgRef("dev.agent-up.desktop", layout.DesktopPackagePath),
                PkgRef("dev.agent-up.cli", layout.CliPackagePath),
                PkgRef("dev.agent-up.server", layout.ServerPackagePath),
                new XElement("choices-outline",
                    new XElement("line", new XAttribute("choice", "desktop")),
                    new XElement("line", new XAttribute("choice", "cli")),
                    new XElement("line", new XAttribute("choice", "server"))),
                Choice("desktop", "Agent-Up Desktop", "dev.agent-up.desktop"),
                Choice("cli", "Agent-Up CLI", "dev.agent-up.cli"),
                Choice("server", "Agent-Up Server", "dev.agent-up.server")))
            .ToString() + Environment.NewLine;

    private static XElement PkgRef(string id, string path)
        => new("pkg-ref", new XAttribute("id", id), new XAttribute("version", "0"), new XAttribute("onConclusion", "none"), Path.GetFileName(path));

    private static XElement Choice(string id, string title, string pkgRef)
        => new("choice",
            new XAttribute("id", id),
            new XAttribute("title", title),
            new XElement("pkg-ref", new XAttribute("id", pkgRef)));
}
