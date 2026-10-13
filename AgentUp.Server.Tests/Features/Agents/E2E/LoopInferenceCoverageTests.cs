using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Features.Agents.Unit;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

[TestFixture]
[Category("LoopInference")]
[Timeout(1_800_000)]
[CancelAfter(1_800_000)]
public sealed class LoopInferenceCoverageTests
{
    [Test]
    public void CoverageScenarios_areAgentAgnosticAndIncludeAnInvariantOnlyCase()
    {
        var root = RepositoryRoot();
        var scenarios = new LoopScenarioCatalog().Load(Path.Join(root, "loop-coverage", "scenarios"));
        var baseline = new LoopOutcomeBaselineStore().Read(
            Path.Join(root, "loop-coverage", "baselines", "cpu-inference.json"));

        Assert.Multiple(() =>
        {
            Assert.That(scenarios, Is.Not.Empty);
            Assert.That(scenarios.Any(scenario => scenario.ScoresOutcome), Is.True);
            Assert.That(scenarios.Any(scenario => !scenario.ScoresOutcome), Is.True);
            Assert.That(scenarios.Select(scenario => scenario.Id), Is.Unique);
            Assert.That(baseline.Model, Is.EqualTo("qwen2.5-1.5b-instruct-q4-k-m"));
            Assert.That(baseline.Samples, Is.EqualTo(scenarios.Count(scenario => scenario.ScoresOutcome)));
        });
    }

    [Test]
    public async Task CpuInference_holdsHardInvariantsAndMatchesThePooledBaseline()
    {
        var root = RepositoryRoot();
        var agent = ResolveAgent();
        var seed = ReadSeed();
        TestContext.Out.WriteLine($"loop-inference seed={seed}");
        TestContext.Out.WriteLine($"loop-inference agent={agent}");

        var scenarios = new LoopScenarioCatalog().Load(Path.Join(root, "loop-coverage", "scenarios"));
        var store = new LoopOutcomeBaselineStore();
        var baseline = store.Read(Path.Join(root, "loop-coverage", "baselines", "cpu-inference.json"));
        var scored = new List<bool>();
        var violations = new List<string>();

        foreach (var scenario in scenarios)
        {
            var result = await RunScenarioAsync(agent, seed, scenario.Prompt);
            var broken = new LoopInvariantGuard().Evaluate(result);
            violations.AddRange(broken.Select(item => $"{scenario.Id}: {item}"));
            TestContext.Out.WriteLine(
                $"{scenario.Id} terminal={result.Terminal} completed={result.CompletedTask} stop={result.StopReason}");
            if (scenario.ScoresOutcome)
                scored.Add(result.Terminal == LoopTerminal.Completed && result.CompletedTask);
        }

        var observed = scored.Count == 0 ? 0 : scored.Count(success => success) / (double)scored.Count;
        TestContext.Out.WriteLine($"loop-inference pooled={observed:0.000} baseline={baseline.PooledSuccessRate} n={scored.Count}");

        Assert.Multiple(() =>
        {
            Assert.That(violations, Is.Empty);
            Assert.That(scored, Has.Count.EqualTo(baseline.Samples));
            Assert.That(store.WithinTolerance(baseline, observed), Is.True);
        });
    }

    private static async Task<LoopRunResult> RunScenarioAsync(string agent, uint seed, string prompt)
    {
        var worktree = Directory.CreateTempSubdirectory("agent-up-loop-inference").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Join(worktree, "README.md"), "loop coverage worktree\n");
            var registry = new WorkspaceRegistry(
                new InMemoryWorkspaceRepository(),
                new PortsController(new InMemoryPortAllocationService()),
                new CapabilitiesController(new CapabilityReconciliationService()),
                new WorkspaceEventBus());
            await registry.StartAsync(CancellationToken.None);
            var workspace = await registry.RegisterAsync(
                ServerDomain.Workspace().Named("LoopCoverage").At(worktree).AtCommit("abc").Build());
            var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Agents:Codex:Command"] = agent,
                    ["Agents:Codex:Arguments:0"] = "--agent",
                    ["Agents:Codex:Arguments:1"] = "test-agent-infer"
                }).Build());
            var events = new AgentEventService(new AgentEventFrameProvider());
            await using var scheduling = new AgentSchedulingService(
                new WorkspaceQueryController(registry),
                new AgentProcessFactory(commands, NullLoggerFactory.Instance),
                commands,
                new FakeSubscriptionLoginProvider(),
                new LoopInferenceEnvironment(seed),
                new FakeClaudeCredentialStore(),
                new AgentSubscriptionAuth(),
                new AgentEventFrameProvider(),
                events,
                NullLogger<AgentSchedulingService>.Instance);
            var scheduled = await scheduling.ScheduleAsync(workspace.Id, "codex", CancellationToken.None);
            if (scheduled.Error is not null)
                throw new InvalidOperationException(scheduled.Error);
            var loop = new AutonomousLoopService(scheduling, new LoopInvariantGuard());
            return await loop.RunAsync(
                new LoopRunRequest(
                    workspace.Id,
                    prompt,
                    3,
                    new AllowListedPermissionPolicy("allow-once", "allow-all"),
                    100_000,
                    seed),
                CancellationToken.None);
        }
        finally
        {
            try { Directory.Delete(worktree, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TestContext.Out.WriteLine($"Could not remove {worktree}: {exception.Message}");
            }
        }
    }

    private static string ResolveAgent()
    {
        var configured = Environment.GetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_AGENT");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var published = Path.Join(RepositoryRoot(), "artifacts", "loop-inference", "test-agents", "AgentUp.TestAgents");
        if (File.Exists(published))
            return published;

        throw new InvalidOperationException(
            "Set AGENTUP_LOOP_INFERENCE_AGENT to the published test-agent-infer executable, or run .github/scripts/run-loop-inference.sh.");
    }

    private static uint ReadSeed()
    {
        var raw = Environment.GetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_SEED");
        return uint.TryParse(raw, out var seed) ? seed : 42;
    }

    private static string RepositoryRoot()
    {
        var directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Join(directory, "agent-up.sln"))
                && Directory.Exists(Path.Join(directory, "loop-coverage")))
                return directory;
            var parent = Directory.GetParent(directory)?.FullName;
            if (parent == directory)
                break;
            directory = parent;
        }

        throw new InvalidOperationException("Could not locate the Agent-Up repository root.");
    }

    private sealed class LoopInferenceEnvironment(uint seed) : IAgentProcessEnvironmentProvider
    {
        public IReadOnlyDictionary<string, string> EnvironmentFor(string agent)
        {
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["AGENTUP_TEST_AGENT"] = "test-agent-infer",
                ["AGENTUP_LOOP_INFERENCE_SEED"] = seed.ToString()
            };
            var models = Environment.GetEnvironmentVariable("AGENTUP_LOOP_INFERENCE_MODEL_ROOT");
            if (!string.IsNullOrWhiteSpace(models))
                environment["AGENTUP_LOOP_INFERENCE_MODEL_ROOT"] = models;
            return environment;
        }
    }
}
