namespace AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

public sealed record SmokeCommandRequest(
    string Command,
    string Platform,
    string RuntimeId,
    string ArtifactDirectory,
    string WorkDirectory,
    string? PayloadRoot);
