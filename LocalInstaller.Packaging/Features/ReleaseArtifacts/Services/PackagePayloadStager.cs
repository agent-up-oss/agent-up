using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;
using LocalInstaller.Packaging.Shared.Interfaces;

namespace LocalInstaller.Packaging.Features.ReleaseArtifacts.Services;

public sealed class PackagePayloadStager
{
    private readonly IPackagePublisher _publisher;
    private readonly IPackageFileSystem _files;

    public PackagePayloadStager(IPackagePublisher publisher, IPackageFileSystem files)
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
            if (request.ProductManifest.InstallerOptions.Count > 0)
            {
                if (request.ProductManifest.InstallerApplication is not null && staging.InstallerPublishDirectory is not null)
                {
                    await _publisher.PublishDotNetProjectAsync(
                        Path.Join(request.RepositoryRoot, request.ProductManifest.InstallerApplication.SourceProjectPath!),
                        request.RuntimeId,
                        request.Configuration,
                        request.Version,
                        staging.InstallerPublishDirectory,
                        cancellationToken);
                }

                await PublishManifestPayloadsAsync(staging, cancellationToken);
                MirrorFirstTargetPayloads(staging);
                return;
            }

            if (staging.InstallerPublishDirectory is not null)
            {
                await _publisher.PublishDotNetProjectAsync(
                    Path.Join(request.RepositoryRoot, "AgentUp.InstallerApp", "AgentUp.InstallerApp.csproj"),
                    request.RuntimeId,
                    request.Configuration,
                    request.Version,
                    staging.InstallerPublishDirectory,
                    cancellationToken);
            }

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
            await _publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, "AgentUp.Tray", "AgentUp.Tray.csproj"),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                staging.TrayPublishDirectory,
                cancellationToken);
            return;
        }

        if (request.ProductManifest.InstallerOptions.Count > 0)
        {
            if (request.ProductManifest.InstallerApplication is not null && staging.InstallerPublishDirectory is not null)
            {
                _publisher.CopyPrebuiltPayload(
                    Path.Join(request.PayloadRoot!, PayloadDirectoryName(request.ProductManifest.InstallerApplication)),
                    staging.InstallerPublishDirectory);
            }

            CopyManifestPayloads(staging);
            MirrorFirstTargetPayloads(staging);
            return;
        }

        if (staging.InstallerPublishDirectory is not null)
            _publisher.CopyPrebuiltPayload(request.InstallerPayloadDirectory!, staging.InstallerPublishDirectory);

        _publisher.CopyPrebuiltPayload(request.DesktopPayloadDirectory!, staging.DesktopPublishDirectory);
        _publisher.CopyPrebuiltPayload(request.ServerPayloadDirectory!, staging.ServerPublishDirectory);
        _publisher.CopyPrebuiltPayload(request.CliPayloadDirectory!, staging.CliPublishDirectory);
        _publisher.CopyPrebuiltPayload(request.TrayPayloadDirectory!, staging.TrayPublishDirectory);
    }

    private async Task PublishManifestPayloadsAsync(PayloadStagingRequest staging, CancellationToken cancellationToken)
    {
        var request = staging.Package;
        foreach (var option in request.ProductManifest.InstallerOptions)
        {
            if (string.IsNullOrWhiteSpace(option.SourceProjectPath))
                throw new InvalidOperationException($"Installer option '{option.Id}' must provide a source project path for local publishing.");

            await _publisher.PublishDotNetProjectAsync(
                Path.Join(request.RepositoryRoot, option.SourceProjectPath),
                request.RuntimeId,
                request.Configuration,
                request.Version,
                FlatStageDirectory(request, option),
                cancellationToken);
        }
    }

    private void CopyManifestPayloads(PayloadStagingRequest staging)
    {
        var request = staging.Package;
        foreach (var option in request.ProductManifest.InstallerOptions)
        {
            _publisher.CopyPrebuiltPayload(
                Path.Join(request.PayloadRoot!, PayloadDirectoryName(option)),
                FlatStageDirectory(request, option));
        }
    }

    private void MirrorFirstTargetPayloads(PayloadStagingRequest staging)
    {
        MirrorFirstTargetPayload(staging, LocalInstallerArtifactTarget.Desktop, staging.DesktopPublishDirectory);
        MirrorFirstTargetPayload(staging, LocalInstallerArtifactTarget.Server, staging.ServerPublishDirectory);
        MirrorFirstTargetPayload(staging, LocalInstallerArtifactTarget.Cli, staging.CliPublishDirectory);
        MirrorFirstTargetPayload(staging, LocalInstallerArtifactTarget.Tray, staging.TrayPublishDirectory);
    }

    private void MirrorFirstTargetPayload(PayloadStagingRequest staging, LocalInstallerArtifactTarget target, string destination)
    {
        var option = staging.Package.ProductManifest.InstallerOptions.FirstOrDefault(component => ComponentTarget(component) == target);
        if (option is null)
            return;

        var source = FlatStageDirectory(staging.Package, option);
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return;

        _publisher.CopyPrebuiltPayload(source, destination);
    }

    private static string FlatStageDirectory(PackageRequest request, PackageProductArtifact option)
        => Path.Join(request.StageDirectory, PayloadDirectoryName(option));

    private static string PayloadDirectoryName(PackageProductArtifact option)
        => string.IsNullOrWhiteSpace(option.PayloadDirectoryName) ? option.Id : option.PayloadDirectoryName;

    private static LocalInstallerArtifactTarget ComponentTarget(PackageProductArtifact component)
        => component.Target;
}
