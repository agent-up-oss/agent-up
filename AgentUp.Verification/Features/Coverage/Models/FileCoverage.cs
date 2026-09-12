namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// Line-level coverage for one source file, as reported by a coverage tool.
/// </summary>
/// <param name="Path">Repo-relative, forward-slashed path.</param>
/// <param name="LineHits">
/// Coverable line number to hit count. A line absent from this map is not coverable —
/// blank lines, comments and declarations the compiler emits no sequence point for — and
/// must never count against a coverage ratio.
/// </param>
public sealed record FileCoverage(string Path, IReadOnlyDictionary<int, int> LineHits)
{
    public bool IsCoverable(int line) => LineHits.ContainsKey(line);

    public bool IsCovered(int line) => LineHits.TryGetValue(line, out var hits) && hits > 0;

    /// <summary>
    /// Merges two reports for the same file by summing hits. A line covered by any test
    /// project is covered, which is what makes multi-report merging correct: a line in
    /// AgentUp.Server may be reached by AgentUp.Server.Tests or by AgentUp.Tests.
    /// </summary>
    public FileCoverage MergeWith(FileCoverage other)
    {
        var merged = new Dictionary<int, int>(LineHits);

        foreach (var (line, hits) in other.LineHits)
            merged[line] = merged.TryGetValue(line, out var existing) ? existing + hits : hits;

        return new FileCoverage(Path, merged);
    }
}
