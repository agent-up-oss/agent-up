using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Fake;

internal sealed class StubCoverageConfigurationLoader(CoverageConfiguration configuration)
    : ICoverageConfigurationLoader
{
    public CoverageConfiguration Load(string repositoryRoot) => configuration;
}

internal sealed class StaticCoverageReportReader(CoverageReport report) : ICoverageReportReader
{
    public Task<CoverageReport> ReadAsync(
        string repositoryRoot,
        string reportDirectory,
        CancellationToken cancellationToken)
        => Task.FromResult(report);
}

internal sealed class StaticChangedLineSource(ChangedLines lines) : IChangedLineSource
{
    public string Name => "test";

    public Task<ChangedLines> GetChangedLinesAsync(string repositoryRoot, CancellationToken cancellationToken)
        => Task.FromResult(lines);
}
