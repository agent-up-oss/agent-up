namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PackageCommandResult(int ExitCode, string? ErrorMessage = null);
