using LocalInstaller.Smoke.Features.PackageValidation.DTOs;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceSmokeResult(
    string? ServerUrl,
    IReadOnlyList<SmokeFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != FindingSeverity.Error);
}
