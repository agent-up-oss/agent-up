using LocalInstaller.Smoke.Shared.Providers;

namespace LocalInstaller.Smoke.Features.PackageValidation.DTOs;

public sealed record PackageValidationRequest
{
    public PackageValidationRequest(string Platform, string RuntimeId, string ArtifactDirectory, string WorkDirectory,
        PackageProductManifest? ProductConfig = null)
    {
        this.Platform = SafeSmokePaths.Identifier(Platform, nameof(Platform));
        this.RuntimeId = SafeSmokePaths.Identifier(RuntimeId, nameof(RuntimeId));
        this.ArtifactDirectory = SafeSmokePaths.Root(ArtifactDirectory, nameof(ArtifactDirectory));
        this.WorkDirectory = SafeSmokePaths.Root(WorkDirectory, nameof(WorkDirectory));
        this.ProductConfig = ProductConfig;
    }

    public string Platform { get; }

    public string RuntimeId { get; }

    public string ArtifactDirectory { get; }

    public string WorkDirectory { get; }

    public PackageProductManifest? ProductConfig { get; }

    public PackageProductManifest Product => ProductConfig ?? LocalInstallerProduct;

    private static readonly PackageProductManifest LocalInstallerProduct = new(
        ServiceName: "localinstaller-server",
        CliShimName: "localinstaller",
        ArtifactBaseName: "localinstaller",
        DisplayName: "LocalInstaller",
        InstallDirName: "LocalInstaller");
}
