using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.SmokeRuns.DTOs;

public sealed record SmokeCommandResult(bool Succeeded, IReadOnlyList<SmokeFinding> Findings);
