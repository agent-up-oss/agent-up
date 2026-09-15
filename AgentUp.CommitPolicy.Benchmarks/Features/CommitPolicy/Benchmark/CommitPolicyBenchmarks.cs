using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.CommitPolicy.Benchmarks.Features.CommitPolicy.Benchmark;

[MemoryDiagnoser]
public class CommitPolicyBenchmarks
{
    private readonly CommitPolicyProvider _policy = new();
    private readonly string[] _files = Enumerable.Range(0, 100).Select(index => $"AgentUp.Server/Features/Workspaces/Services/WorkspaceService{index}.cs").ToArray();

    [Benchmark]
    public void ValidateHundredFileSlice() => _policy.Validate("Workspaces", "fix(Workspaces): update lifecycle", _files);
}
