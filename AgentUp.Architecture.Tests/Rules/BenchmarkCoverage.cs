using AgentUp.Architecture.Tests.Fixtures;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Keeps designated performance-sensitive code connected to a benchmark that the
/// verification engine actually executes. The rule intentionally checks for benchmark
/// methods rather than accepting an empty folder as performance coverage.
/// </summary>
[TestFixture]
public sealed class BenchmarkCoverage
{
    private static readonly BenchmarkTarget[] RequiredBenchmarks =
    [
        new(
            "AgentUp.Browser.Streaming/BrowserInputParser.cs",
            "benchmarks/baselines/browser-streaming.json",
            "AgentUp.Browser.Streaming.Benchmarks.Features.Input.Benchmark.BrowserInputParserBenchmarks.MouseMove",
            "browser-streaming-benchmarks"),
        new(
            "AgentUp.Server/Features/Agents/Providers/AgentEventFrameProvider.cs",
            "benchmarks/baselines/server-agents.json",
            "AgentUp.Server.Benchmarks.Features.Agents.Benchmark.AgentEventFrameBenchmarks.SerializeFourKilobyteAgentEvent",
            "server-benchmarks"),
        new(
            "AgentUp.Mobile/src/features/agents/providers/AgentEventPresentationProvider.ts",
            "benchmarks/baselines/mobile.json",
            "groupTranscript1000",
            "mobile-benchmarks"),
        new(
            "AgentUp.Mobile/src/features/git/providers/GitChangeTreeProvider.ts",
            "benchmarks/baselines/mobile.json",
            "flattenGitTree1000",
            "mobile-benchmarks")
    ];

    [Test]
    public void Designated_performance_targets_have_numeric_regression_baselines()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = RequiredBenchmarks
            .Where(target => !BaselineContains(root, target))
            .Select(target => $"{target.ProductionPath} requires numeric baseline '{target.BenchmarkId}' in {target.BaselinePath}")
            .ToArray();

        Assert.That(violations, Is.Empty,
            "A benchmark designation must have a stored numeric baseline consumed by its performance gate.");
    }

    [Test]
    public void Designated_performance_targets_select_their_slow_verification_check()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Join(root, "agent-up.json")));
        var verification = document.RootElement.GetProperty("verification");
        var checks = verification.GetProperty("checks");
        var paths = verification.GetProperty("paths").EnumerateArray().ToArray();

        var violations = RequiredBenchmarks
            .Where(target => !checks.TryGetProperty(target.CheckId, out var check)
                || check.GetProperty("tier").GetString() != "slow"
                || !paths.Any(rule => ProductionRuleSelects(rule, target)))
            .Select(target => $"{target.ProductionPath} is not connected to slow check '{target.CheckId}'")
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Designated performance code must select its slow benchmark check so verification receipts invalidate when it changes.");
    }

    [Test]
    public void Ci_executes_every_performance_gate_and_the_test_kind_watchdog()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var workflow = File.ReadAllText(Path.Join(root, ".github", "workflows", "ci.yml"));
        var requiredCommands = new[]
        {
            "./scripts/run-benchmark-gate.sh browser",
            "./scripts/run-benchmark-gate.sh server",
            "MobilePerformanceGate.ts",
            "./.github/scripts/run-test-kind-watchdog.sh"
        };

        var missing = requiredCommands.Where(command => !workflow.Contains(command, StringComparison.Ordinal)).ToArray();

        Assert.That(missing, Is.Empty,
            "Performance checks that only exist in local verification are not gates; CI must execute all of them.");
    }

    private static bool BaselineContains(string root, BenchmarkTarget target)
    {
        var path = Path.Join(root, target.BaselinePath);
        if (!File.Exists(path)) return false;
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.GetProperty("benchmarks").TryGetProperty(target.BenchmarkId, out var baseline))
            return false;
        return (baseline.TryGetProperty("meanNanoseconds", out var nanoseconds) && nanoseconds.GetDouble() > 0
                || baseline.TryGetProperty("meanMicroseconds", out var microseconds) && microseconds.GetDouble() > 0)
               && baseline.GetProperty("maxTimeRatio").GetDouble() >= 1;
    }

    private static bool ProductionRuleSelects(System.Text.Json.JsonElement rule, BenchmarkTarget target)
        => target.ProductionPath.StartsWith(
               (rule.GetProperty("match").GetString() ?? string.Empty).TrimEnd('*'),
               StringComparison.Ordinal)
           && rule.GetProperty("checks").EnumerateArray()
               .Any(check => check.GetString() == target.CheckId);

    private sealed record BenchmarkTarget(string ProductionPath, string BaselinePath, string BenchmarkId, string CheckId);
}
