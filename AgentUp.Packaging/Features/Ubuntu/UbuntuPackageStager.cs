using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.Ubuntu;

public sealed class UbuntuPackageStager
{
    private readonly IPackageWriter _writer;

    public UbuntuPackageStager(IPackageWriter writer)
    {
        _writer = writer;
    }

    public void Stage(PackageRequest request, UbuntuPackageLayout layout, UbuntuPackageManifest manifest)
    {
        _writer.ResetDirectory(layout.DebRoot);
        _writer.CreateDirectory(Path.Combine(layout.DebRoot, "DEBIAN"));
        _writer.CreateDirectory(Path.Combine(layout.DebRoot, "opt", "agent-up"));
        _writer.CreateDirectory(Path.Combine(layout.DebRoot, "etc", "systemd", "system"));
        _writer.CreateDirectory(Path.Combine(layout.DebRoot, "usr", "bin"));
        _writer.CreateDirectory(Path.Combine(layout.DebRoot, "usr", "share", "applications"));
        _writer.CreateDirectory(Path.Combine(layout.DebRoot, "usr", "share", "pixmaps"));

        _writer.CopyDirectory(layout.DesktopPublishDirectory, Path.Combine(layout.DebRoot, "opt", "agent-up", "desktop"));
        _writer.CopyDirectory(layout.ServerPublishDirectory, Path.Combine(layout.DebRoot, "opt", "agent-up", "server"));
        _writer.CopyDirectory(layout.CliPublishDirectory, Path.Combine(layout.DebRoot, "opt", "agent-up", "cli"));
        _writer.CopyFile(
            Path.Combine(request.RepositoryRoot, "packaging", "linux", manifest.ServiceName),
            Path.Combine(layout.DebRoot, "etc", "systemd", "system", manifest.ServiceName));
        _writer.CopyFile(
            Path.Combine(request.RepositoryRoot, "media", "logo.png"),
            Path.Combine(layout.DebRoot, "usr", "share", "pixmaps", "agent-up.png"));

        _writer.CreateSymbolicLink(Path.Combine(layout.DebRoot, "usr", "bin", "agent-up"), manifest.CliSymlinkTarget);
        _writer.WriteText(Path.Combine(layout.DebRoot, "usr", "share", "applications", "agent-up.desktop"), manifest.DesktopEntryText());
        _writer.WriteText(Path.Combine(layout.DebRoot, "DEBIAN", "control"), manifest.ControlFileText());
        _writer.WriteText(Path.Combine(layout.DebRoot, "DEBIAN", "postinst"), UbuntuPackageManifest.PostInstallScript());
        _writer.WriteText(Path.Combine(layout.DebRoot, "DEBIAN", "prerm"), UbuntuPackageManifest.PreRemoveScript());
        _writer.WriteText(Path.Combine(layout.DebRoot, "DEBIAN", "postrm"), UbuntuPackageManifest.PostRemoveScript());

        _writer.SetExecutable(Path.Combine(layout.DebRoot, "opt", "agent-up", "desktop", "AgentUp.Desktop"));
        _writer.SetExecutable(Path.Combine(layout.DebRoot, "opt", "agent-up", "server", "AgentUp.Server"));
        _writer.SetExecutable(Path.Combine(layout.DebRoot, "opt", "agent-up", "cli", "AgentUp.CLI"));
        _writer.SetExecutable(Path.Combine(layout.DebRoot, "DEBIAN", "postinst"));
        _writer.SetExecutable(Path.Combine(layout.DebRoot, "DEBIAN", "prerm"));
        _writer.SetExecutable(Path.Combine(layout.DebRoot, "DEBIAN", "postrm"));
    }
}
