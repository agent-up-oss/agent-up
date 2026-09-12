using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// Builds line coverage for one file without a test spelling out a hit dictionary.
/// </summary>
internal sealed class FileCoverageBuilder(string path)
{
    private readonly Dictionary<int, int> _hits = [];

    public FileCoverageBuilder Covered(params int[] lines)
    {
        foreach (var line in lines)
            _hits[line] = 1;

        return this;
    }

    public FileCoverageBuilder Uncovered(params int[] lines)
    {
        foreach (var line in lines)
            _hits[line] = 0;

        return this;
    }

    /// <summary>Marks lines as present in the file but not coverable, by omitting them.</summary>
    public FileCoverageBuilder NotCoverable(params int[] lines)
    {
        foreach (var line in lines)
            _hits.Remove(line);

        return this;
    }

    public FileCoverage Build() => new(path, _hits);
}
