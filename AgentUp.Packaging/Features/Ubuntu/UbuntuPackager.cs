using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.Ubuntu;

public sealed class UbuntuPackager
{
    private readonly ICommandRunner _commands;
    private readonly IPackageWriter _writer;

    public UbuntuPackager(ICommandRunner commands, IPackageWriter writer)
    {
        _commands = commands;
        _writer = writer;
    }

    public async Task PackageAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        var layout = UbuntuPackageLayout.From(request);
        var manifest = UbuntuPackageManifest.From(request);
        await new PackagePayloadStager(_commands, _writer).StageAsync(
            request,
            layout.DesktopPublishDirectory,
            layout.ServerPublishDirectory,
            layout.CliPublishDirectory,
            cancellationToken);

        new UbuntuPackageStager(_writer).Stage(request, layout, manifest);
        await _commands.RunAsync(new CommandSpec("dpkg-deb", ["--build", layout.DebRoot, layout.DebOutputPath]), cancellationToken);
    }
}
