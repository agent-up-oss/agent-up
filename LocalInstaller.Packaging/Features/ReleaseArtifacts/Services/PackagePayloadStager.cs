using LocalInstaller.Core.Shared.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;
using LocalInstaller.Packaging.Shared.Interfaces;
using LocalInstaller.Packaging.Shared.Providers;

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
            if (request.ProductManifest.InstallerOptions.Count == 0)
                throw new InvalidOperationException("LocalInstaller.Packaging requires installer options to publish payloads.");

            var installer = request.ProductManifest.InstallerApplication
                ?? throw new InvalidOperationException("LocalInstaller.Packaging requires an installer application manifest.");

            if (staging.InstallerPublishDirectory is not null)
                await _publisher.PublishDotNetProjectAsync(
                    SourceProjectPath(request, installer),
                    request.RuntimeId,
                    request.Configuration,
                    request.Version,
                    staging.InstallerPublishDirectory,
                    cancellationToken);

            await PublishManifestPayloadsAsync(staging, cancellationToken);
            MirrorFirstTargetPayloads(staging);
            return;
        }

        if (request.ProductManifest.InstallerOptions.Count > 0)
        {
            if (staging.InstallerPublishDirectory is not null)
            {
                var installer = request.ProductManifest.InstallerApplication
                    ?? throw new InvalidOperationException("LocalInstaller.Packaging requires an installer application manifest.");

                _publisher.CopyPrebuiltPayload(
                    Path.Join(request.PayloadRoot!, PayloadDirectoryName(installer)),
                    staging.InstallerPublishDirectory);
            }

            CopyManifestPayloads(staging);
            MirrorFirstTargetPayloads(staging);
            return;
        }

        throw new InvalidOperationException("LocalInstaller.Packaging requires installer options to copy prebuilt payloads.");
    }

    private async Task PublishManifestPayloadsAsync(PayloadStagingRequest staging, CancellationToken cancellationToken)
    {
        var request = staging.Package;
        foreach (var option in request.ProductManifest.InstallerOptions)
        {
            if (string.IsNullOrWhiteSpace(option.SourceProjectPath))
                throw new InvalidOperationException($"Installer option '{option.Id}' must provide a source project path for local publishing.");

            await _publisher.PublishDotNetProjectAsync(
                SourceProjectPath(request, option),
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

    private static string SourceProjectPath(PackageRequest request, PackageProductArtifact option)
        => PackagePathValidator.ResolveRelativeUnderRoot(request.RepositoryRoot, option.SourceProjectPath, nameof(option.SourceProjectPath));

    private static LocalInstallerArtifactTarget ComponentTarget(PackageProductArtifact component)
        => component.Target;
}
