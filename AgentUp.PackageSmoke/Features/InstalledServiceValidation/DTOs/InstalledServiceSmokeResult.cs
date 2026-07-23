using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceSmokeResult(
    string? ServerUrl,
    IReadOnlyList<SmokeFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != FindingSeverity.Error);
}
