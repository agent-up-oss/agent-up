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
            DebRoot: Path.Combine(stage, "deb-root"),
            DebOutputPath: Path.Combine(request.OutputRoot, $"agent-up-ubuntu-{request.RuntimeId}.deb"),
            DesktopPublishDirectory: Path.Combine(stage, "desktop"),
            ServerPublishDirectory: Path.Combine(stage, "server"),
            CliPublishDirectory: Path.Combine(stage, "cli"));
    }
}
