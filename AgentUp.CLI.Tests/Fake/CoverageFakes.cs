using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class StubCoverageConfigurationLoader(CoverageConfiguration configuration)
    : ICoverageConfigurationLoader
{
    public CoverageConfiguration Load(string repositoryRoot) => configuration;
}

/// <summary>
/// A loader for a broken 'coverage' section, so the command layer's handling of it can be
/// asserted rather than surfacing as an unhandled exception.
/// </summary>
internal sealed class ThrowingCoverageConfigurationLoader(string message) : ICoverageConfigurationLoader
{
    public CoverageConfiguration Load(string repositoryRoot) => throw new CoverageConfigurationException(message);
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
