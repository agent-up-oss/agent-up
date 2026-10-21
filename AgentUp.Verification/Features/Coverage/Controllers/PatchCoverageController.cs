using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Services;

namespace AgentUp.Verification.Features.Coverage.Controllers;

/// <summary>Slice boundary for measuring coverage of changed lines.</summary>
public sealed class PatchCoverageController(PatchCoverageService coverage)
{
    public Task<PatchCoverageResult> MeasureAsync(
        string repositoryRoot,
        double? minimumOverride,
        CancellationToken cancellationToken)
        => coverage.MeasureAsync(repositoryRoot, minimumOverride, cancellationToken);
}
