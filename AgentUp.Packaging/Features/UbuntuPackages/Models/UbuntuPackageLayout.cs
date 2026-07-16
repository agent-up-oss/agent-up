using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.UbuntuPackages.Models;

public sealed record UbuntuPackageLayout(
    string DebRoot,
    string DebOutputPath,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory)
{
    public static UbuntuPackageLayout From(PackageRequest request)
    {
        var stage = request.StageDirectory;
        return new UbuntuPackageLayout(
            DebRoot: Path.Join(stage, "deb-root"),
            DebOutputPath: Path.Join(request.OutputRoot, $"agent-up-ubuntu-{request.RuntimeId}.deb"),
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"));
    }
}
