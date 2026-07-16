using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.Ubuntu;

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
