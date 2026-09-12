using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Support;

internal sealed class ChangedLinesBuilder
{
    private readonly Dictionary<string, IReadOnlySet<int>> _lines = new(StringComparer.Ordinal);

    public static ChangedLinesBuilder Changing(string path, params int[] lines)
        => new ChangedLinesBuilder().With(path, lines);

    public ChangedLinesBuilder With(string path, params int[] lines)
    {
        _lines[path] = lines.ToHashSet();
        return this;
    }

    public ChangedLines Build() => new(_lines);
}
