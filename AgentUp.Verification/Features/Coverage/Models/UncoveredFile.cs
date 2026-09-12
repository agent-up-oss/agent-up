namespace AgentUp.Verification.Features.Coverage.Models;

/// <summary>
/// The changed-but-uncovered lines of one file, so a report can name exactly what to test
/// rather than only a percentage.
/// </summary>
public sealed record UncoveredFile(string Path, IReadOnlyList<int> Lines)
{
    /// <summary>Collapses consecutive line numbers into ranges for readable output.</summary>
    public string DescribeRanges()
    {
        var ordered = Lines.Order().ToArray();
        var ranges = new List<string>();
        var start = 0;

        for (var index = 0; index < ordered.Length; index++)
        {
            var isLast = index == ordered.Length - 1;
            var breaksRun = isLast || ordered[index + 1] != ordered[index] + 1;
            if (!breaksRun)
                continue;

            ranges.Add(ordered[start] == ordered[index]
                ? ordered[start].ToString()
                : $"{ordered[start]}-{ordered[index]}");
            start = index + 1;
        }

        return string.Join(", ", ranges);
    }
}
