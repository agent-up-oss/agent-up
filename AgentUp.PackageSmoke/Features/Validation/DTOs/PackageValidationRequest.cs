namespace AgentUp.PackageSmoke.Features.Validation.DTOs;

public sealed record PackageValidationRequest(
    string Platform,
    string RuntimeId,
    string ArtifactDirectory,
    string WorkDirectory);
