using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Coverage.Services;

/// <summary>
/// Measures coverage of the changed lines only.
/// </summary>
/// <remarks>
/// This is the half the verification guard cannot do on its own. The guard proves a check
/// ran against the current bytes; it cannot tell whether the tests it ran reach the new
/// code. Restricting the measurement to changed lines is what makes the number actionable
/// on a large existing codebase: total coverage barely moves per change, patch coverage
/// moves immediately.
/// </remarks>
public sealed class PatchCoverageService(
    ICoverageConfigurationLoader loader,
    ICoverageReportReader reports,
    IEnumerable<IChangedLineSource> sources,
    PathGlobProvider globs)
{
    public async Task<PatchCoverageResult> MeasureAsync(
        string repositoryRoot,
        double? minimumOverride = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = loader.Load(repositoryRoot);
        var minimum = minimumOverride is { } requested ? Bounded(requested) : configuration.Minimum;

        if (!configuration.IsConfigured)
            return PatchCoverageResult.NothingToCover(minimum);

        var changed = await CollectChangedLinesAsync(repositoryRoot, cancellationToken);
        var measured = Measured(configuration, changed);

        if (measured.Count == 0)
            return PatchCoverageResult.NothingToCover(minimum);

        var report = await reports.ReadAsync(repositoryRoot, configuration.ReportDirectory, cancellationToken);

        var withReport = measured
            .Select(pair => (pair.Path, pair.Lines, Coverage: report.Find(pair.Path)))
            .ToArray();

        // A file absent from every report blocks only when its whole project is absent
        // too, which means that suite never ran. A file whose project is covered but which
        // has no entry of its own simply has no executable code.
        var missing = withReport
            .Where(item => item.Coverage is null && !report.CoversProjectOf(item.Path))
            .Select(item => item.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var evaluated = withReport
            .Where(item => item.Coverage is not null)
            .Select(item => Evaluate(item.Path, item.Lines, item.Coverage!))
            .ToArray();

        return new PatchCoverageResult(
            evaluated.Sum(file => file.Covered),
            evaluated.Sum(file => file.Coverable),
            minimum,
            [.. evaluated.Where(file => file.Uncovered.Count > 0)
                .Select(file => new UncoveredFile(file.Path, file.Uncovered))
                .OrderBy(file => file.Path, StringComparer.Ordinal)],
            missing);
    }

    /// <summary>
    /// Applies the include and exclude globs. Include decides what the gate is about at
    /// all; exclude removes files where a coverage number carries no information.
    /// </summary>
    private IReadOnlyList<(string Path, IReadOnlySet<int> Lines)> Measured(
        CoverageConfiguration configuration,
        ChangedLines changed)
        => [.. changed.Lines
            .Where(pair => configuration.Include.Any(glob => globs.Matches(glob, pair.Key)))
            .Where(pair => !configuration.Exclude.Any(glob => globs.Matches(glob, pair.Key)))
            .Select(pair => (Path: pair.Key, Lines: pair.Value))
            .OrderBy(pair => pair.Path, StringComparer.Ordinal)];

    private static (string Path, int Covered, int Coverable, IReadOnlyList<int> Uncovered) Evaluate(
        string path,
        IReadOnlySet<int> changedLines,
        FileCoverage coverage)
    {
        // Only coverable lines count. A changed line the compiler emits no sequence point
        // for - a blank line, a brace, a record declaration - is neither covered nor a
        // failure, and including it would make the ratio depend on formatting.
        var coverable = changedLines.Where(coverage.IsCoverable).ToArray();
        var uncovered = coverable.Where(line => !coverage.IsCovered(line)).Order().ToArray();

        return (path, coverable.Length - uncovered.Length, coverable.Length, uncovered);
    }

    private async Task<ChangedLines> CollectChangedLinesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var contributions = new List<ChangedLines>();

        foreach (var source in sources)
            contributions.Add(await source.GetChangedLinesAsync(repositoryRoot, cancellationToken));

        return contributions.Aggregate(ChangedLines.None, (merged, next) => merged.MergeWith(next));
    }

    /// <summary>
    /// Validates a caller-supplied minimum. The configuration loader bounds the value it
    /// reads, but --min bypasses it entirely: ReadMinimum parses any double, so a negative,
    /// above-hundred or non-finite override would be compared against coverage directly and
    /// make every slice pass or fail regardless of its tests.
    /// </summary>
    private static double Bounded(double minimum)
        => double.IsFinite(minimum) && minimum is >= 0d and <= 100d
            ? minimum
            : throw new CoverageConfigurationException(
                $"A coverage minimum must be a number between 0 and 100, not {minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
}
