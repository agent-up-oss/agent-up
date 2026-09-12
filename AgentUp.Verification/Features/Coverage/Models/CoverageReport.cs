namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// Merged coverage across every report found, keyed by repo-relative path.
/// </summary>
public sealed record CoverageReport(IReadOnlyDictionary<string, FileCoverage> Files)
{
    public static readonly CoverageReport Empty =
        new(new Dictionary<string, FileCoverage>(StringComparer.Ordinal));

    public static CoverageReport FromFiles(IEnumerable<FileCoverage> files)
        => new(files
            .GroupBy(file => file.Path, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Aggregate((left, right) => left.MergeWith(right)),
                StringComparer.Ordinal));

    public CoverageReport MergeWith(CoverageReport other)
        => FromFiles(Files.Values.Concat(other.Files.Values));

    /// <summary>
    /// Whether any report carries data for the project a path belongs to, keyed by first
    /// path segment.
    /// </summary>
    /// <remarks>
    /// Distinguishes the two very different reasons a changed file can be missing from
    /// every report: its test project never ran, which must block, or the file simply has
    /// no executable code - an interface, an enum, a declaration-only record - which is
    /// benign. Without this the gate would fail on a changed interface.
    /// </remarks>
    public bool CoversProjectOf(string path)
    {
        var project = ProjectOf(path);
        return project.Length > 0 && Files.Keys.Any(covered =>
            string.Equals(ProjectOf(covered), project, StringComparison.Ordinal));
    }

    private static string ProjectOf(string path)
    {
        var separator = path.IndexOf('/', StringComparison.Ordinal);
        return separator < 0 ? path : path[..separator];
    }

    public FileCoverage? Find(string path)
        => Files.TryGetValue(path, out var file) ? file : null;
}
