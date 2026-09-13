using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Coverage.Services;

/// <summary>
/// Measures total line coverage per feature slice.
/// </summary>
/// <remarks>
/// The counterpart to patch coverage rather than a replacement for it. Patch coverage keeps
/// each change honest but says nothing about a slice that was thin before the gate existed;
/// this reports where that debt actually sits, which the architecture suite cannot do - it
/// runs before the suites that produce the reports, so all it can check is that tests exist
/// in the right folders.
/// </remarks>
public sealed class SliceCoverageService(
    ICoverageConfigurationLoader loader,
    ICoverageReportReader reports,
    PathGlobProvider globs)
{
    private const string FeaturesSegment = "Features";

    public async Task<SliceCoverageResult> MeasureAsync(
        string repositoryRoot,
        double? minimumOverride = null,
        CancellationToken cancellationToken = default)
    {
        var configuration = loader.Load(repositoryRoot);
        var minimum = minimumOverride is { } requested ? Bounded(requested) : configuration.SliceMinimum;

        var report = await reports.ReadAsync(repositoryRoot, configuration.ReportDirectory, cancellationToken);

        var slices = report.Files.Values
            .Where(file => configuration.Include.Any(glob => globs.Matches(glob, file.Path)))
            .Where(file => !configuration.Exclude.Any(glob => globs.Matches(glob, file.Path)))
            .Select(file => (Slice: SliceOf(file.Path), File: file))
            .Where(item => item.Slice.Length > 0)
            .GroupBy(item => item.Slice, StringComparer.Ordinal)
            .Select(group => Total(group.Key, group.Select(item => item.File)))
            .OrderBy(slice => slice.Percent)
            .ThenBy(slice => slice.Slice, StringComparer.Ordinal)
            .ToArray();

        if (slices.Length == 0)
            return SliceCoverageResult.NothingMeasured(minimum);

        var exempt = configuration.SliceExemptions.ToHashSet(StringComparer.Ordinal);

        return new SliceCoverageResult(
            slices,
            minimum,
            [.. slices.Where(slice => !slice.Meets(minimum) && !exempt.Contains(slice.Slice))],
            [.. slices.Where(slice => slice.Meets(minimum) && exempt.Contains(slice.Slice))
                .Select(slice => slice.Slice)
                .Order(StringComparer.Ordinal)]);
    }

    private static SliceCoverage Total(string slice, IEnumerable<FileCoverage> files)
        => files.Aggregate(
            new SliceCoverage(slice, 0, 0),
            (total, file) => total.Add(
                file.LineHits.Values.Count(hits => hits > 0),
                file.LineHits.Count));

    /// <summary>
    /// The slice a path belongs to, or empty for a file outside a feature slice - a shared
    /// type, a composition root, or a project with no slice layout at all.
    /// </summary>
    private static string SliceOf(string path)
    {
        var parts = path.Split('/');
        return parts.Length >= 4 && parts[1] == FeaturesSegment
            ? $"{parts[0]}/{FeaturesSegment}/{parts[2]}"
            : string.Empty;
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
