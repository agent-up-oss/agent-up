using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Features.Coverage.Interfaces;

/// <summary>
/// Loads and merges every coverage report a test run produced.
/// </summary>
public interface ICoverageReportReader
{
    Task<CoverageReport> ReadAsync(
        string repositoryRoot,
        string reportDirectory,
        CancellationToken cancellationToken);
}
