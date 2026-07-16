using AgentUp.PackageSmoke.Features.Validation.DTOs;

namespace AgentUp.PackageSmoke.Features.InstalledServices.DTOs;

public sealed record InstalledServiceSmokeResult(
    string? ServerUrl,
    IReadOnlyList<SmokeFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != FindingSeverity.Error);
}
