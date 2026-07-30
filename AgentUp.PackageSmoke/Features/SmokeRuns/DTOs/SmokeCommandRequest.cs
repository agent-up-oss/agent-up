using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

public sealed record SmokeCommandRequest
{
    public SmokeCommandRequest(
        string Command,
        string Platform,
        string RuntimeId,
        string ArtifactDirectory,
        string WorkDirectory,
        string? PayloadRoot,
        SmokeProductManifest? ProductManifest = null)
    {
        this.Command = SafeSmokePaths.Identifier(Command, nameof(Command));
        this.Platform = SafeSmokePaths.Identifier(Platform, nameof(Platform));
        this.RuntimeId = string.IsNullOrWhiteSpace(RuntimeId)
            ? string.Empty
            : SafeSmokePaths.Identifier(RuntimeId, nameof(RuntimeId));
        this.ArtifactDirectory = string.IsNullOrWhiteSpace(ArtifactDirectory)
            ? string.Empty
            : SafeSmokePaths.Root(ArtifactDirectory, nameof(ArtifactDirectory));
        this.WorkDirectory = SafeSmokePaths.Root(WorkDirectory, nameof(WorkDirectory));
        this.PayloadRoot = PayloadRoot is null ? null : SafeSmokePaths.Root(PayloadRoot, nameof(PayloadRoot));
        this.ProductManifest = ProductManifest;
    }

    public string Command { get; }

    public string Platform { get; }

    public string RuntimeId { get; }

    public string ArtifactDirectory { get; }

    public string WorkDirectory { get; }

    public string? PayloadRoot { get; }

    public SmokeProductManifest? ProductManifest { get; }

#if EXCLUDE_AGENTUP_PRODUCT_CONFIGURATION
    public SmokeProductManifest Product => ProductManifest ?? throw new InvalidOperationException("Generic package smoke requests require an explicit product manifest.");
#else
    public SmokeProductManifest Product => ProductManifest ?? SmokeProductManifest.AgentUp;
#endif
}
