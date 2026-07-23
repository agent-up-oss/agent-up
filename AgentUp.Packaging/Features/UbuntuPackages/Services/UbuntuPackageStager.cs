using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.UbuntuPackages.Models;
using AgentUp.Packaging.Features.UbuntuPackages.Providers;

namespace AgentUp.Packaging.Features.UbuntuPackages.Services;

public sealed class UbuntuPackageStager
{
    private readonly IPackageWriter _writer;

    public UbuntuPackageStager(IPackageWriter writer)
    {
        _writer = writer;
    }

    public void Stage(PackageRequest request, UbuntuPackageLayout layout, UbuntuPackageManifest manifest)
    {
        var pkg = manifest.PackageName;
        var installerDir = Path.Join(layout.DebRoot, "opt", pkg, "installer");
        var payloadDir = Path.Join(installerDir, "payload");

        _writer.ResetDirectory(layout.DebRoot);
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "DEBIAN"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "usr", "share", "applications"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "usr", "share", "metainfo"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "usr", "share", "pixmaps"));

        _writer.CopyDirectory(layout.InstallerPublishDirectory, installerDir);
        _writer.CopyDirectory(layout.DesktopPublishDirectory, Path.Join(payloadDir, "desktop"));
        _writer.CopyDirectory(layout.ServerPublishDirectory, Path.Join(payloadDir, "server"));
        _writer.CopyDirectory(layout.CliPublishDirectory, Path.Join(payloadDir, "cli"));
        _writer.CopyFile(
            Path.Join(request.RepositoryRoot, "packaging", "linux", manifest.ServiceName),
            Path.Join(payloadDir, "service", manifest.ServiceName));
        _writer.CopyFile(
            Path.Join(request.RepositoryRoot, "media", "logo.png"),
            Path.Join(payloadDir, "icon", "Agent-Up.png"));
        _writer.CopyFile(
            Path.Join(request.RepositoryRoot, "media", "logo.png"),
            Path.Join(layout.DebRoot, "usr", "share", "pixmaps", $"{pkg}.png"));

        _writer.SetExecutable(Path.Join(installerDir, "AgentUp.InstallerApp"));

        _writer.WriteText(
            Path.Join(layout.DebRoot, "usr", "share", "applications", $"{pkg}-installer.desktop"),
            manifest.InstallerDesktopEntryText());
        _writer.WriteText(
            Path.Join(layout.DebRoot, "usr", "share", "metainfo", $"{pkg}-installer.desktop.metainfo.xml"),
            manifest.MetainfoText());

        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "control"), manifest.ControlFileText());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "postinst"), manifest.PostInstallScript());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "prerm"), manifest.PreRemoveScript());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "postrm"), UbuntuPackageManifest.PostRemoveScript());
        _writer.SetExecutable(Path.Join(layout.DebRoot, "DEBIAN", "postinst"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "DEBIAN", "prerm"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "DEBIAN", "postrm"));
    }
}
