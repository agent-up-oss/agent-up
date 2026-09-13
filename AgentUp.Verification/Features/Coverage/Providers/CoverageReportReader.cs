using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Features.Coverage.Providers;

/// <summary>
/// Finds the current coverage.cobertura.xml for each check under the report directory and
/// merges them.
/// </summary>
/// <remarks>
/// Merging across checks matters: each test project writes its own report, and a production
/// line may be reached by more than one of them, so taking any single report would
/// understate coverage.
/// <para>
/// Within one check only the newest run counts. The test runner adds a fresh GUID folder
/// per run without removing the previous one, so a check that has run twice leaves two
/// reports. The older one describes the file as it was before the edit, and its line
/// numbers no longer line up: merging it in resurrects lines that have since moved or gone,
/// each with zero hits, which reads as uncovered code that does not exist.
/// </para>
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
            .GroupBy(report => CheckFolderOf(searchRoot, report), StringComparer.Ordinal)
            .Select(NewestRun)
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

    /// <summary>
    /// The check a report belongs to: the folder directly below the report root, which is
    /// where each check is configured to write. A report sitting at the root is its own
    /// group.
    /// </summary>
    private static string CheckFolderOf(string searchRoot, string reportPath)
    {
        var relative = Path.GetRelativePath(searchRoot, reportPath);
        var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return separator < 0 ? string.Empty : relative[..separator];
    }

    /// <summary>Ordered by path as well as time so equal timestamps still pick one report.</summary>
    private static string NewestRun(IGrouping<string, string> runs)
        => runs.OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(report => report, StringComparer.Ordinal)
            .First();
}
