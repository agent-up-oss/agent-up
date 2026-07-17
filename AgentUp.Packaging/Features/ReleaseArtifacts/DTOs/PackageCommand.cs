namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PackageCommand(
    string Platform,
    string RuntimeId,
    string Version,
    string OutputDirectory,
    string? PayloadRoot);
