namespace AgentUp.PackageSmoke.Features.Validation;

public sealed record PackageValidationRequest(
    string Platform,
    string RuntimeId,
    string ArtifactDirectory,
    string WorkDirectory);
