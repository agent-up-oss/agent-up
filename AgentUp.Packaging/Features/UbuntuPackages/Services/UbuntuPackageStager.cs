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

        _writer.ResetDirectory(layout.DebRoot);
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "DEBIAN"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "opt", pkg));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "etc", "systemd", "system"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "usr", "bin"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "usr", "share", "applications"));
        _writer.CreateDirectory(Path.Join(layout.DebRoot, "usr", "share", "pixmaps"));

        _writer.CopyDirectory(layout.DesktopPublishDirectory, Path.Join(layout.DebRoot, "opt", pkg, "desktop"));
        _writer.CopyDirectory(layout.ServerPublishDirectory, Path.Join(layout.DebRoot, "opt", pkg, "server"));
        _writer.CopyDirectory(layout.CliPublishDirectory, Path.Join(layout.DebRoot, "opt", pkg, "cli"));
        _writer.CopyFile(
            Path.Join(request.RepositoryRoot, "packaging", "linux", manifest.ServiceName),
            Path.Join(layout.DebRoot, "etc", "systemd", "system", manifest.ServiceName));
        _writer.CopyFile(
            Path.Join(request.RepositoryRoot, "media", "logo.png"),
            Path.Join(layout.DebRoot, "usr", "share", "pixmaps", $"{pkg}.png"));

        _writer.CreateSymbolicLink(Path.Join(layout.DebRoot, "usr", "bin", pkg), manifest.CliSymlinkTarget);
        _writer.WriteText(Path.Join(layout.DebRoot, "usr", "share", "applications", $"{pkg}.desktop"), manifest.DesktopEntryText());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "control"), manifest.ControlFileText());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "postinst"), manifest.PostInstallScript());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "prerm"), manifest.PreRemoveScript());
        _writer.WriteText(Path.Join(layout.DebRoot, "DEBIAN", "postrm"), UbuntuPackageManifest.PostRemoveScript());

        _writer.SetExecutable(Path.Join(layout.DebRoot, "opt", pkg, "desktop", "AgentUp.Desktop"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "opt", pkg, "server", "AgentUp.Server"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "opt", pkg, "cli", "AgentUp.CLI"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "DEBIAN", "postinst"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "DEBIAN", "prerm"));
        _writer.SetExecutable(Path.Join(layout.DebRoot, "DEBIAN", "postrm"));
    }
}
