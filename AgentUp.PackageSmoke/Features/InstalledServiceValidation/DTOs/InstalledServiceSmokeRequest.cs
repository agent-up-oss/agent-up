using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;
using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceSmokeRequest
{
    public InstalledServiceSmokeRequest(
        string Platform,
        string RuntimeId,
        string ArtifactDirectory,
        string WorkDirectory,
        string PrimaryServerUrl = "http://127.0.0.1:5000",
        string FallbackServerUrl = "http://localhost:5000",
        SmokeProductConfig? ProductConfig = null,
        string SystemRoot = "/")
    {
        this.Platform = SafeSmokePaths.Identifier(Platform, nameof(Platform));
        this.RuntimeId = SafeSmokePaths.Identifier(RuntimeId, nameof(RuntimeId));
        this.ArtifactDirectory = SafeSmokePaths.Root(ArtifactDirectory, nameof(ArtifactDirectory));
        this.WorkDirectory = SafeSmokePaths.Root(WorkDirectory, nameof(WorkDirectory));
        this.PrimaryServerUrl = PrimaryServerUrl;
        this.FallbackServerUrl = FallbackServerUrl;
        this.ProductConfig = ProductConfig;
        this.SystemRoot = SafeSmokePaths.Root(SystemRoot, nameof(SystemRoot));
    }

    public string Platform { get; }

    public string RuntimeId { get; }

    public string ArtifactDirectory { get; }

    public string WorkDirectory { get; }

    public string PrimaryServerUrl { get; }

    public string FallbackServerUrl { get; }

    public SmokeProductConfig? ProductConfig { get; }

    public string SystemRoot { get; }

    public SmokeProductConfig Product => ProductConfig ?? SmokeProductConfig.AgentUp;
}
