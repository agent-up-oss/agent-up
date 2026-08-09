using LocalInstaller.Core.Shared.Models;

namespace LocalInstaller.Core.Features.Installation.Models;

public static class LocalInstallerManifestComponentExtensions
{
    public static ProductComponent ToProductComponent(this LocalInstallerArtifactManifest manifest)
        => ToProductComponent(manifest.ToDescriptor());

    public static ProductComponent ToProductComponent(this LocalInstallerArtifactDescriptor descriptor)
        => new(
            descriptor.Id,
            descriptor.DisplayName,
            descriptor.Description,
            TargetFor(descriptor.Target),
            descriptor.ExecutableName,
            descriptor.PayloadDirectoryName,
            descriptor.SourceProjectPath);

    private static InstallerComponentTarget TargetFor(LocalInstallerArtifactTarget target)
        => target switch
        {
            LocalInstallerArtifactTarget.Desktop => InstallerComponentTarget.Desktop,
            LocalInstallerArtifactTarget.Server => InstallerComponentTarget.Server,
            LocalInstallerArtifactTarget.Cli => InstallerComponentTarget.Cli,
            LocalInstallerArtifactTarget.Tray => InstallerComponentTarget.Tray,
            LocalInstallerArtifactTarget.InstallerApp => InstallerComponentTarget.InstallerApp,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
}
