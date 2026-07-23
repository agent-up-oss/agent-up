using AgentUp.PackageSmoke.Shared.Providers;

namespace AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

public sealed record PackageValidationRequest
{
    public PackageValidationRequest(string Platform, string RuntimeId, string ArtifactDirectory, string WorkDirectory)
    {
        this.Platform = SafeSmokePaths.Identifier(Platform, nameof(Platform));
        this.RuntimeId = SafeSmokePaths.Identifier(RuntimeId, nameof(RuntimeId));
        this.ArtifactDirectory = SafeSmokePaths.Root(ArtifactDirectory, nameof(ArtifactDirectory));
        this.WorkDirectory = SafeSmokePaths.Root(WorkDirectory, nameof(WorkDirectory));
    }

    public string Platform { get; }

    public string RuntimeId { get; }

    public string ArtifactDirectory { get; }

    public string WorkDirectory { get; }
}
