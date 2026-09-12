using System.Globalization;
using System.Text.RegularExpressions;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Coverage.Providers;

/// <summary>
/// Reads the added and modified line numbers out of a unified diff.
/// </summary>
/// <remarks>
/// Run with --unified=0 so each hunk header describes exactly the changed run. The header
/// is "@@ -oldStart[,oldCount] +newStart[,newCount] @@": an omitted count means one line,
/// and a newCount of zero means a pure deletion, which contributes no lines because a
/// change cannot be asked to cover code it removed.
/// </remarks>
public sealed class UnifiedDiffParser
{
    private static readonly Regex FileHeader = new(
        @"^\+\+\+ (?:b/)?(?<path>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HunkHeader = new(
        @"^@@ -\d+(?:,\d+)? \+(?<start>\d+)(?:,(?<count>\d+))? @@", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ChangedLines Parse(string diff)
    {
        var byPath = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        string? current = null;

        foreach (var line in diff.Split('\n').Select(entry => entry.TrimEnd('\r')))
        {
            var fileMatch = FileHeader.Match(line);
            if (fileMatch.Success)
            {
                var path = fileMatch.Groups["path"].Value.Trim();
                current = path == "/dev/null" ? null : PathGlobProvider.Normalize(path);
                continue;
            }

            var hunkMatch = HunkHeader.Match(line);
            if (!hunkMatch.Success || current is null)
                continue;

            AddHunk(byPath, current, hunkMatch);
        }

        return ChangedLines.FromPairs(
            byPath.Select(pair => new KeyValuePair<string, IReadOnlySet<int>>(pair.Key, pair.Value)));
    }

    private static void AddHunk(Dictionary<string, HashSet<int>> byPath, string path, Match hunk)
    {
        var start = int.Parse(hunk.Groups["start"].Value, CultureInfo.InvariantCulture);
        var countGroup = hunk.Groups["count"];
        var count = countGroup.Success
            ? int.Parse(countGroup.Value, CultureInfo.InvariantCulture)
            : 1;

        if (count == 0)
            return;

        if (!byPath.TryGetValue(path, out var lines))
        {
            lines = [];
            byPath[path] = lines;
        }

        foreach (var line in Enumerable.Range(start, count))
            lines.Add(line);
    }
}
