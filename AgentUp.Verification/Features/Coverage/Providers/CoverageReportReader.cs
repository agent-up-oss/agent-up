using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Features.Coverage.Providers;

/// <summary>
/// Finds every coverage.cobertura.xml under the report directory and merges them.
/// </summary>
/// <remarks>
/// Merging matters: each test project writes its own report, and a production line may be
/// reached by more than one of them. Taking any single report would understate coverage.
/// </remarks>
public sealed class CoverageReportReader(CoberturaReportParser parser) : ICoverageReportReader
{
    private const string ReportFileName = "coverage.cobertura.xml";

    public async Task<CoverageReport> ReadAsync(
        string repositoryRoot,
        string reportDirectory,
        CancellationToken cancellationToken)
    {
        var searchRoot = Path.Join(repositoryRoot, reportDirectory);
        if (!Directory.Exists(searchRoot))
            return CoverageReport.Empty;

        var reports = Directory.EnumerateFiles(searchRoot, ReportFileName, SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var files = new List<FileCoverage>();

        foreach (var report in reports)
        {
            var xml = await File.ReadAllTextAsync(report, cancellationToken);
            files.AddRange(parser.Parse(xml, repositoryRoot));
        }

        return CoverageReport.FromFiles(files);
    }
}
