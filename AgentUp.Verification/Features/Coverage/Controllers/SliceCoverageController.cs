using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Services;

namespace AgentUp.Verification.Features.Coverage.Controllers;

/// <summary>Slice boundary for measuring total coverage of each feature slice.</summary>
public sealed class SliceCoverageController(SliceCoverageService coverage)
{
    public Task<SliceCoverageResult> MeasureAsync(
        string repositoryRoot,
        double? minimumOverride,
        CancellationToken cancellationToken)
        => coverage.MeasureAsync(repositoryRoot, minimumOverride, cancellationToken);
}
