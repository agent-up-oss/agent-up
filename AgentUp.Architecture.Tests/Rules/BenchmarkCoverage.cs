using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace AgentUp.Architecture.Tests.Rules;

/// <summary>
/// Keeps every designated hot path connected to a numeric baseline, receipt inputs,
/// verification selection, and the CI job that executes the performance gate.
/// </summary>
[TestFixture]
public sealed class BenchmarkCoverage
{
    private static readonly BenchmarkTarget[] RequiredBenchmarks =
    [
        BrowserTarget("MouseMove"),
        BrowserTarget("Click"),
        BrowserTarget("KeyDown"),
        BrowserTarget("ControlMode"),
        new(
            "AgentUp.Server/Features/Agents/Providers/AgentEventFrameProvider.cs",
            "AgentUp.Server",
            "AgentUp.Server.Benchmarks/Features/Agents/Benchmark/AgentEventFrameBenchmarks.cs",
            "AgentUp.Server.Benchmarks",
            "benchmarks/baselines/server-agents.json",
            "AgentUp.Server.Benchmarks.Features.Agents.Benchmark.AgentEventFrameBenchmarks.SerializeFourKilobyteAgentEvent",
            "server-benchmarks"),
        new(
            "AgentUp.Mobile/src/features/agents/providers/AgentEventPresentationProvider.ts",
            "AgentUp.Mobile",
            "AgentUp.Mobile/src/features/agents/benchmark/MobileViewBenchmarks.ts",
            "AgentUp.Mobile",
            "benchmarks/baselines/mobile.json",
            "groupTranscript1000",
            "mobile-benchmarks"),
        new(
            "AgentUp.Mobile/src/features/git/providers/GitChangeTreeProvider.ts",
            "AgentUp.Mobile",
            "AgentUp.Mobile/src/features/agents/benchmark/MobileViewBenchmarks.ts",
            "AgentUp.Mobile",
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
    public void Designated_benchmark_sources_register_executable_benchmarks()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = RequiredBenchmarks
            .Select(target => target.BenchmarkPath)
            .Distinct(StringComparer.Ordinal)
            .Where(path => !HasExecutableBenchmark(Path.Join(root, path)))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Benchmark sources must register an executable public instance BenchmarkDotNet method or a Mitata bench call.");
    }

    [Test]
    public void Designated_performance_targets_select_slow_checks_with_complete_receipt_inputs()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Join(root, "agent-up.json")));
        var verification = document.RootElement.GetProperty("verification");
        var checks = verification.GetProperty("checks");
        var paths = verification.GetProperty("paths").EnumerateArray().ToArray();

        var violations = RequiredBenchmarks
            .Where(target => !CheckCoversTarget(checks, paths, target))
            .Select(target => $"{target.CheckId} must be slow, include {target.ProductionInput} and {target.BenchmarkInput}, and be selected by both {target.ProductionPath} and {target.BenchmarkPath}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Performance receipts must cover the measured production and benchmark inputs, and both paths must select the gate.");
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
            "./.github/scripts/run-test-kind-watchdog.sh",
            "needs: [platform, performance-gates, docs, jetbrains-plugin, version, helm-chart]"
        };

        var missing = requiredCommands.Where(command => !workflow.Contains(command, StringComparison.Ordinal)).ToArray();

        Assert.That(missing, Is.Empty,
            "CI must execute every performance gate and prevent release when a gate fails.");
    }

    private static BenchmarkTarget BrowserTarget(string method) => new(
        "AgentUp.Browser.Streaming/BrowserInputParser.cs",
        "AgentUp.Browser.Streaming",
        "AgentUp.Browser.Streaming.Benchmarks/Features/Input/Benchmark/BrowserInputParserBenchmarks.cs",
        "AgentUp.Browser.Streaming.Benchmarks",
        "benchmarks/baselines/browser-streaming.json",
        $"AgentUp.Browser.Streaming.Benchmarks.Features.Input.Benchmark.BrowserInputParserBenchmarks.{method}",
        "browser-streaming-benchmarks");

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

    private static bool CheckCoversTarget(
        System.Text.Json.JsonElement checks,
        IReadOnlyList<System.Text.Json.JsonElement> paths,
        BenchmarkTarget target)
    {
        if (!checks.TryGetProperty(target.CheckId, out var check)
            || check.GetProperty("tier").GetString() != "slow")
            return false;

        var inputs = check.GetProperty("inputs").EnumerateArray()
            .Select(input => input.GetString())
            .ToHashSet(StringComparer.Ordinal);
        return inputs.Contains(target.ProductionInput)
               && inputs.Contains(target.BenchmarkInput)
               && paths.Any(rule => RuleSelectsPath(rule, target.ProductionPath, target.CheckId))
               && paths.Any(rule => RuleSelectsPath(rule, target.BenchmarkPath, target.CheckId));
    }

    private static bool HasExecutableBenchmark(string path)
        => Path.GetExtension(path) == ".ts"
            ? HasMitataBenchmark(File.ReadAllText(path))
            : ArchitectureFixture.ParseSourceFile(path).Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(IsExecutableBenchmarkMethod);

    private static bool IsExecutableBenchmarkMethod(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers.Select(modifier => modifier.Kind()).ToHashSet();
        var attributes = method.AttributeLists.SelectMany(list => list.Attributes)
            .Select(attribute => ArchitectureFixture.FinalTypeSegment(attribute.Name))
            .ToHashSet(StringComparer.Ordinal);
        var declaringType = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var argumentsDeclared = attributes.Contains("Arguments") || attributes.Contains("ArgumentsSource");
        return attributes.Contains("Benchmark")
               && modifiers.Contains(SyntaxKind.PublicKeyword)
               && !modifiers.Contains(SyntaxKind.StaticKeyword)
               && method.TypeParameterList is null
               && (method.ParameterList.Parameters.Count == 0 || argumentsDeclared)
               && declaringType is not null
               && declaringType.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword))
               && declaringType.Modifiers.All(modifier => !modifier.IsKind(SyntaxKind.SealedKeyword));
    }

    private static bool HasMitataBenchmark(string source)
    {
        var withoutComments = Regex.Replace(source, @"/\*[\s\S]*?\*/|//[^\r\n]*", string.Empty);
        var importsBench = Regex.IsMatch(
            withoutComments,
            "import\\s*\\{[^}]*\\bbench\\b[^}]*\\}\\s*from\\s*['\\\"]mitata['\\\"]\\s*;",
            RegexOptions.CultureInvariant);
        return importsBench && Regex.IsMatch(
            withoutComments,
            @"(?:^|[;{}])\s*bench\s*\(",
            RegexOptions.CultureInvariant);
    }

    private static bool RuleSelectsPath(System.Text.Json.JsonElement rule, string path, string checkId)
        => path.StartsWith((rule.GetProperty("match").GetString() ?? string.Empty).TrimEnd('*'), StringComparison.Ordinal)
           && rule.GetProperty("checks").EnumerateArray().Any(check => check.GetString() == checkId);

    private sealed record BenchmarkTarget(
        string ProductionPath,
        string ProductionInput,
        string BenchmarkPath,
        string BenchmarkInput,
        string BaselinePath,
        string BenchmarkId,
        string CheckId);
}
