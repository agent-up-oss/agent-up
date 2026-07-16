using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Services;

public sealed class PackagePayloadStager
{
    private readonly PackagePublisher _publisher;
    private readonly IPackageFileSystem _files;

    public PackagePayloadStager(PackagePublisher publisher, IPackageFileSystem files)
    {
        _publisher = publisher;
        _files = files;
    }

    public async Task StageAsync(PayloadStagingRequest staging, CancellationToken cancellationToken = default)
    {
        var request = staging.Package;

        _files.ResetDirectory(request.StageDirectory);
        _files.CreateDirectory(request.OutputRoot);

        if (request.PayloadRoot is null)
        {
            await _publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Desktop", "AgentUp.Desktop.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                staging.DesktopPublishDirectory,
                cancellationToken);
            await _publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Server", "AgentUp.Server.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                staging.ServerPublishDirectory,
                cancellationToken);
            await _publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.CLI", "AgentUp.CLI.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                staging.CliPublishDirectory,
                cancellationToken);
            return;
        }

        _publisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, staging.DesktopPublishDirectory);
        _publisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, staging.ServerPublishDirectory);
        _publisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, staging.CliPublishDirectory);
    }
}
