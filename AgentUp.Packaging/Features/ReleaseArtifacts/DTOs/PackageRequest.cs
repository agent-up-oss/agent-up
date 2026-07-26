using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PackageRequest
{
    public PackageRequest(
        string repositoryRoot,
        string platform,
        string runtimeId,
        string version,
        string outputDirectory,
        string configuration,
        string? payloadRoot = null,
        PackageProductManifest? productManifest = null)
    {
        RepositoryRoot = PackagePathValidator.RequireFullyQualifiedPath(repositoryRoot, nameof(RepositoryRoot));
        Platform = platform;
        RuntimeId = runtimeId;
        Version = version;
        OutputDirectory = outputDirectory;
        Configuration = configuration;

        PackagePathValidator.RequireSafePathComponent(Platform, nameof(Platform));
        PackagePathValidator.RequireSafePathComponent(RuntimeId, nameof(RuntimeId));
        PackagePathValidator.RequireSafeRelativePath(OutputDirectory, nameof(OutputDirectory));
        PayloadRoot = payloadRoot is null
            ? null
            : PackagePathValidator.ResolveRootOrRelativeUnderRoot(RepositoryRoot, payloadRoot!, nameof(PayloadRoot));
        ProductManifest = productManifest ?? PackageProductManifest.AgentUp();
        PackageProductManifest.Validate(ProductManifest);
        PackagePathValidator.RequireSafePathComponent(ProductManifest.Slug, nameof(ProductManifest.Slug));
    }

    public string RepositoryRoot { get; init; }
    public string Platform { get; init; }
    public string RuntimeId { get; init; }
    public string Version { get; init; }
    public string OutputDirectory { get; init; }
    public string Configuration { get; init; }
    public string? PayloadRoot { get; init; }
    public PackageProductManifest ProductManifest { get; init; }

    public string NormalizedVersion => Version.TrimStart('v', 'V');
    public string WindowsInstallerVersion => NormalizeWindowsInstallerVersion(NormalizedVersion);
    public string StageDirectory => PackagePathValidator.ResolveRelativeUnderRoot(RepositoryRoot, Path.Join("artifacts", "stage", PlatformRuntimeDirectory), nameof(StageDirectory));
    public string OutputRoot => PackagePathValidator.ResolveRelativeUnderRoot(RepositoryRoot, OutputDirectory, nameof(OutputDirectory));
    public string? PayloadRootDirectory => PayloadRoot;
    public string? InstallerPayloadDirectory => PayloadRootDirectory is null ? null : Path.Join(PayloadRootDirectory, "installer");
    public string? DesktopPayloadDirectory => PayloadRootDirectory is null ? null : Path.Join(PayloadRootDirectory, "desktop");
    public string? ServerPayloadDirectory => PayloadRootDirectory is null ? null : Path.Join(PayloadRootDirectory, "server");
    public string? CliPayloadDirectory => PayloadRootDirectory is null ? null : Path.Join(PayloadRootDirectory, "cli");
    public string? TrayPayloadDirectory => PayloadRootDirectory is null ? null : Path.Join(PayloadRootDirectory, "tray");

    private string PlatformRuntimeDirectory =>
        $"{PackagePathValidator.RequireSafePathComponent(Platform, nameof(Platform))}-{PackagePathValidator.RequireSafePathComponent(RuntimeId, nameof(RuntimeId))}";

    private static string NormalizeWindowsInstallerVersion(string version)
    {
        var core = version.Split(['-', '+'], 2)[0];
        return core == "0.0.0" ? "0.0.1" : core;
    }
}
