using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Verification.Benchmarks.Features.Coverage.Benchmark;

[MemoryDiagnoser]
public class CoverageParserBenchmarks
{
    private readonly UnifiedDiffParser _diffParser = new();
    private readonly CoberturaReportParser _coverageParser = new();
    private readonly string _diff = string.Join('\n', Enumerable.Range(0, 200).SelectMany(index => new[] { $"+++ b/AgentUp.Server/Features/Slice{index % 20}/File{index}.cs", $"@@ -10,2 +10,{index % 8 + 1} @@" }));
    private readonly string _coverage = "<coverage><sources><source>/repo</source></sources><packages><package><classes>" + string.Join(string.Empty, Enumerable.Range(0, 200).Select(index => $"<class filename=\"AgentUp.Server/Features/Slice{index % 20}/File{index}.cs\"><lines><line number=\"10\" hits=\"1\"/><line number=\"11\" hits=\"0\"/></lines></class>")) + "</classes></package></packages></coverage>";

    [Benchmark]
    public ChangedLines ParseTwoHundredFileDiff() => _diffParser.Parse(_diff);

    [Benchmark]
    public IReadOnlyList<FileCoverage> ParseTwoHundredFileCoverageReport() => _coverageParser.Parse(_coverage, "/repo");
}
