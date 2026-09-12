namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// The lines a change added or modified, keyed by repo-relative path.
/// </summary>
/// <remarks>
/// Only lines present in the new revision are recorded. Deleted lines are excluded on
/// purpose: a change cannot be asked to cover code it removed.
/// </remarks>
public sealed record ChangedLines(IReadOnlyDictionary<string, IReadOnlySet<int>> Lines)
{
    public static readonly ChangedLines None =
        new(new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal));

    public static ChangedLines FromPairs(IEnumerable<KeyValuePair<string, IReadOnlySet<int>>> pairs)
        => new(pairs
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<int>)group.SelectMany(pair => pair.Value).ToHashSet(),
                StringComparer.Ordinal));

    public ChangedLines MergeWith(ChangedLines other)
        => FromPairs(Lines.Concat(other.Lines));

    public int TotalLines => Lines.Values.Sum(lines => lines.Count);
}
