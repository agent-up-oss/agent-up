using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Services;

public sealed class PackagePayloadStager
{
    private readonly ICommandRunner _commands;
    private readonly IPackageFileSystem _files;

    public PackagePayloadStager(ICommandRunner commands, IPackageFileSystem files)
    {
        _commands = commands;
        _files = files;
    }

    public async Task StageAsync(
        PackageRequest request,
        string desktopPublishDirectory,
        string serverPublishDirectory,
        string cliPublishDirectory,
        CancellationToken cancellationToken = default)
    {
        var publisher = new PackagePublisher(_commands);

        _files.ResetDirectory(request.StageDirectory);
        _files.CreateDirectory(request.OutputRoot);

        if (request.PayloadRoot is null)
        {
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                desktopPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Server", "AgentUp.Server.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                serverPublishDirectory,
                cancellationToken);
            await publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.CLI", "AgentUp.CLI.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                cliPublishDirectory,
                cancellationToken);
            return;
        }

        PackagePublisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, desktopPublishDirectory);
        PackagePublisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, serverPublishDirectory);
        PackagePublisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, cliPublishDirectory);
    }
}
