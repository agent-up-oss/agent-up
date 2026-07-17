using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Models;
using AgentUp.Packaging.Features.MacOsPackages.Providers;

namespace AgentUp.Packaging.Features.MacOsPackages.Services;

public sealed class MacOsPackageStager
{
    private readonly IMacOsPackageWriter _writer;

    public MacOsPackageStager(IMacOsPackageWriter writer)
    {
        _writer = writer;
    }

    public void Stage(MacOsPackageLayout layout, MacOsPackageManifest manifest)
    {
        var plists = new MacOsPlistGenerator(manifest);

        _writer.ResetDirectory(layout.PackageRootDirectory);
        _writer.CreateDirectory(layout.ComponentPackageDirectory);

        _writer.CopyDirectory(layout.InstallerPublishDirectory, layout.InstallerAppMacOsDirectory);
        _writer.WriteText(layout.InstallerInfoPlistPath, plists.InstallerInfoPlist());
        _writer.SetExecutable(Path.Join(layout.InstallerAppMacOsDirectory, "AgentUp.InstallerApp"));
        _writer.CopyDirectory(layout.DesktopPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "desktop"));
        _writer.CopyDirectory(layout.ServerPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "server"));
        _writer.CopyDirectory(layout.CliPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "cli"));

        _writer.CopyDirectory(layout.DesktopPublishDirectory, layout.DesktopAppMacOsDirectory);
        _writer.WriteText(layout.DesktopInfoPlistPath, plists.DesktopInfoPlist());
        _writer.SetExecutable(Path.Join(layout.DesktopAppMacOsDirectory, "AgentUp.Desktop"));
        _writer.CopyDirectory(layout.DesktopPublishDirectory, Path.Join(layout.DesktopComponentRoot, "usr", "local", "agent-up", "desktop"));
        _writer.SetExecutable(Path.Join(layout.DesktopComponentRoot, "usr", "local", "agent-up", "desktop", "AgentUp.Desktop"));
        _writer.CopyDirectory(layout.CliPublishDirectory, Path.Join(layout.CliComponentRoot, "usr", "local", "agent-up", "cli"));
        _writer.CopyDirectory(layout.ServerPublishDirectory, Path.Join(layout.ServerComponentRoot, "Library", "Application Support", "Agent-Up", "server"));
        _writer.WriteText(layout.LaunchDaemonPlistPath, plists.LaunchDaemonPlist());
        _writer.SetExecutable(Path.Join(layout.ServerComponentRoot, "Library", "Application Support", "Agent-Up", "server", "AgentUp.Server"));
        _writer.WriteText(layout.PreInstallScriptPath, MacOsScriptGenerator.PreInstallScript());
        _writer.WriteText(layout.PostInstallScriptPath, MacOsScriptGenerator.PostInstallScript());
        _writer.SetExecutable(layout.PreInstallScriptPath);
        _writer.SetExecutable(layout.PostInstallScriptPath);
        _writer.WriteText(layout.DistributionXmlPath, MacOsDistributionGenerator.DistributionXml(layout, manifest));
    }
}
