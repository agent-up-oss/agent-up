using AgentUp.Verification.Shared.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Verification.Benchmarks.Features.Verification.Benchmark;

[MemoryDiagnoser]
public class PathGlobBenchmarks
{
    private readonly PathGlobProvider _globs = new();
    private readonly string[] _paths = Enumerable.Range(0, 1_000).Select(index => $"AgentUp.Server/Features/Slice{index % 20}/Providers/File{index}.cs").ToArray();

    [Benchmark]
    public int MatchThousandChangedPaths()
        => _paths.Count(path => _globs.Matches("AgentUp.Server/Features/**/Providers/*.cs", path));
}
