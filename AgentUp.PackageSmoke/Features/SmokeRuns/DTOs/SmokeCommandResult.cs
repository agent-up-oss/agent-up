using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.SmokeRuns.DTOs;

public sealed record SmokeCommandResult(bool Succeeded, IReadOnlyList<SmokeFinding> Findings);
