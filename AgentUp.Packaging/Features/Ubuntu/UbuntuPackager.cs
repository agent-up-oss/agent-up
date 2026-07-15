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
        var publisher = new PackagePublisher(_commands);

        _writer.ResetDirectory(request.StageDirectory);
        _writer.CreateDirectory(request.OutputRoot);
        if (request.PayloadRoot is null)
        {
            await publisher.PublishDotNetProjectAsync(
                Path.Combine(request.RepositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.DesktopPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Combine(request.RepositoryRoot, "AgentUp.Server", "AgentUp.Server.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.ServerPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Combine(request.RepositoryRoot, "AgentUp.CLI", "AgentUp.CLI.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                layout.CliPublishDirectory,
                cancellationToken);
        }
        else
        {
            PackagePublisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, layout.DesktopPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, layout.ServerPublishDirectory);
            PackagePublisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, layout.CliPublishDirectory);
        }

        new UbuntuPackageStager(_writer).Stage(request, layout, manifest);
        await _commands.RunAsync(new CommandSpec("dpkg-deb", ["--build", layout.DebRoot, layout.DebOutputPath]), cancellationToken);
    }
}
