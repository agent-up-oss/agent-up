using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            "AgentUp.Browser.Streaming.Benchmarks/Features/Input/Benchmark/BrowserInputParserBenchmarks.cs",
            "browser-streaming-benchmarks"),
        new(
            "AgentUp.CommitPolicy/Features/CommitPolicy/Providers/CommitPolicyProvider.cs",
            "AgentUp.CommitPolicy.Benchmarks/Features/CommitPolicy/Benchmark/CommitPolicyBenchmarks.cs",
            "commit-policy-benchmarks"),
        new(
            "AgentUp.Server/Features/Applications/Providers/MetricsResponseParser.cs",
            "AgentUp.Server.Benchmarks/Features/Applications/Benchmark/MetricsResponseParserBenchmarks.cs",
            "server-benchmarks"),
        new(
            "AgentUp.Server/Features/Validation/Providers/PlaywrightFlowExporter.cs",
            "AgentUp.Server.Benchmarks/Features/Validation/Benchmark/PlaywrightFlowExporterBenchmarks.cs",
            "server-benchmarks"),
        new(
            "AgentUp.Server/Features/Agents/Providers/AgentEventFrameProvider.cs",
            "AgentUp.Server.Benchmarks/Features/Agents/Benchmark/AgentEventFrameBenchmarks.cs",
            "server-benchmarks"),
        new(
            "AgentUp.Server/Shared/Providers/ConsoleSecretRedactor.cs",
            "AgentUp.Server.Benchmarks/Features/Diagnostics/Benchmark/DiagnosticRedactionBenchmarks.cs",
            "server-benchmarks"),
        new(
            "AgentUp.Server/Features/Metrics/Providers/ProcessMetricsSampler.cs",
            "AgentUp.Server.Benchmarks/Features/Metrics/Benchmark/ProcessMetricsBenchmarks.cs",
            "server-benchmarks"),
        new(
            "AgentUp.Server/Features/Processes/Services/ProcessOutputService.cs",
            "AgentUp.Server.Benchmarks/Features/Processes/Benchmark/BackgroundProcessOutputBenchmarks.cs",
            "server-benchmarks"),
        new(
            "AgentUp.Desktop/Features/Agents/Providers/AgentEventPresentationProvider.cs",
            "AgentUp.Desktop.Benchmarks/Features/Agents/Benchmark/AgentPresentationBenchmarks.cs",
            "desktop-benchmarks"),
        new(
            "AgentUp.Mobile/src/features/agents/providers/AgentEventPresentationProvider.ts",
            "AgentUp.Mobile/src/features/agents/benchmark/MobileViewBenchmarks.ts",
            "mobile-benchmarks"),
        new(
            "AgentUp.Verification/Features/Coverage/Providers/UnifiedDiffParser.cs",
            "AgentUp.Verification.Benchmarks/Features/Coverage/Benchmark/CoverageParserBenchmarks.cs",
            "verification-benchmarks"),
        new(
            "AgentUp.Verification/Shared/Providers/PathGlobProvider.cs",
            "AgentUp.Verification.Benchmarks/Features/Verification/Benchmark/PathGlobBenchmarks.cs",
            "verification-benchmarks")
    ];

    [Test]
    public void Designated_performance_targets_have_executable_benchmarks()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = RequiredBenchmarks
            .Where(target => !HasBenchmarkMethod(Path.Join(root, target.BenchmarkPath)))
            .Select(target => $"{target.ProductionPath} requires a [Benchmark] method in {target.BenchmarkPath}")
            .ToArray();

        Assert.That(violations, Is.Empty,
            "A benchmark designation must measure code; an empty benchmark folder is not coverage.");
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

    private static bool HasBenchmarkMethod(string path)
        => File.Exists(path) && (Path.GetExtension(path) == ".ts"
            ? File.ReadAllText(path).Contains("bench(", StringComparison.Ordinal)
            : ArchitectureFixture.ParseSourceFile(path).Root.DescendantNodes()
               .OfType<MethodDeclarationSyntax>()
               .Any(method => method.AttributeLists.SelectMany(list => list.Attributes)
                   .Any(attribute => ArchitectureFixture.FinalTypeSegment(attribute.Name) == "Benchmark")));

    private static bool ProductionRuleSelects(System.Text.Json.JsonElement rule, BenchmarkTarget target)
        => target.ProductionPath.StartsWith(
               (rule.GetProperty("match").GetString() ?? string.Empty).TrimEnd('*'),
               StringComparison.Ordinal)
           && rule.GetProperty("checks").EnumerateArray()
               .Any(check => check.GetString() == target.CheckId);

    private sealed record BenchmarkTarget(string ProductionPath, string BenchmarkPath, string CheckId);
}
