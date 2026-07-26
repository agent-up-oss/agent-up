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
        _writer.CreateDirectory(layout.InstallerAppResourcesDirectory);
        _writer.CopyFile(layout.InstallerIconSourcePath, layout.InstallerIconPath);
        _writer.CreateDirectory(layout.InstallerPayloadIconDirectory);
        _writer.CopyFile(layout.InstallerIconSourcePath, layout.InstallerPayloadIconPath);
        _writer.WriteText(layout.InstallerInfoPlistPath, plists.InstallerInfoPlist());
        _writer.SetExecutable(Path.Join(layout.InstallerAppMacOsDirectory, "AgentUp.InstallerApp"));
        _writer.CopyDirectory(layout.DesktopPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "desktop"));
        _writer.CopyDirectory(layout.ServerPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "server"));
        _writer.CopyDirectory(layout.CliPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "cli"));
        _writer.CopyDirectory(layout.TrayPublishDirectory, Path.Join(layout.InstallerPayloadDirectory, "tray"));

        var installerPreInstallScriptPath = Path.Join(layout.InstallerScriptsDirectory, "preinstall");
        var installerPostInstallScriptPath = Path.Join(layout.InstallerScriptsDirectory, "postinstall");
        _writer.WriteText(installerPreInstallScriptPath, MacOsScriptGenerator.InstallerPreInstallScript());
        _writer.WriteText(installerPostInstallScriptPath, MacOsScriptGenerator.InstallerPostInstallScript());
        _writer.SetExecutable(installerPreInstallScriptPath);
        _writer.SetExecutable(installerPostInstallScriptPath);
        _writer.WriteText(layout.DistributionXmlPath, MacOsDistributionGenerator.DistributionXml(layout, manifest));
    }
}
