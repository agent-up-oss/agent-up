using AgentUp.Server.Features.Verification.Controllers;
using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Server.Features.Verification.Services;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Verification.Controller;

[TestFixture]
public sealed class VerificationMcpToolsTests
{
    private const string Worktree = "/repo";
    private const string ServerSource = "AgentUp.Server/Features/Git/Services/GitChangesService.cs";
    private const string UnmappedSource = "Experiments/scratch/Prototype.cs";

    private static VerificationConfiguration Rules(VerificationEnforcement enforcement = VerificationEnforcement.Block)
        => new(
            enforcement,
            ["architecture"],
            new Dictionary<string, CheckDefinition>(StringComparer.Ordinal)
            {
                ["architecture"] = new("architecture", "dotnet test Arch", null, CheckTier.Fast, [], false, 0, []),
                ["server"] = new("server", "dotnet test Server", null, CheckTier.Fast, [], false, 0, ["AgentUp.Server"])
            },
            [new VerificationPathRule("AgentUp.Server/**", ["server"])]);

    private static IReadOnlyDictionary<string, string> Changed(params string[] paths)
        => paths.ToDictionary(path => path, path => "sha256:" + path.Length, StringComparer.Ordinal);

    private static VerificationMcpTools ToolsOver(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        InMemoryReceiptLedgerStore ledger,
        ScriptedCheckRunner runner,
        WorkspaceRegistry? registry = null)
    {
        var plans = new VerificationPlanService(
            new StubVerificationConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider("linux")),
            [new StaticChangedContentSource(changed)]);

        return new VerificationMcpTools(new VerificationMcpService(
            plans,
            new VerificationRunService(plans, ledger, runner, new FakeVerificationClock()),
            new VerificationGuardService(plans, ledger),
            new VerificationReportService(),
            ServerTestComposition.CreateWorkspaceTargetController(
                registry ?? ServerTestComposition.CreateRegistry())));
    }

    // Every tool here resolves its target through one shared boundary, so the refusal is
    // asserted once across the tools rather than repeated per tool.
    [Test]
    public async Task NoVerificationTool_actsWhenTheCallNamesNoWorkspace()
    {
        var tools = ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner());

        var results = new[]
        {
            await tools.PlanVerification(),
            await tools.RunVerification(),
            await tools.RunVerificationCheck("server"),
            await tools.GuardVerification()
        };

        Assert.Multiple(() =>
        {
            foreach (var result in results)
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Message, Does.Contain("worktreePath"));
            }
        });
    }

    [Test]
    public async Task PlanVerification_reportsTheChecksTheStaticRulesRequire()
    {
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .PlanVerification(worktreePath: Worktree);

        var plan = (VerificationPlanDto)result.Data!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(plan.Checks.Select(check => check.CheckId), Is.EqualTo(new[] { "architecture", "server" }));
        });
    }

    [Test]
    public async Task PlanVerification_surfacesAConfigurationErrorAsAFailedToolResult()
    {
        var plans = new VerificationPlanService(
            new ThrowingVerificationConfigurationLoader("agent-up.json is not valid JSON"),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider("linux")),
            [new StaticChangedContentSource(Changed(ServerSource))]);
        var ledger = new InMemoryReceiptLedgerStore();
        var tools = new VerificationMcpTools(new VerificationMcpService(
            plans,
            new VerificationRunService(plans, ledger, new ScriptedCheckRunner(), new FakeVerificationClock()),
            new VerificationGuardService(plans, ledger),
            new VerificationReportService(),
            ServerTestComposition.CreateWorkspaceTargetController(ServerTestComposition.CreateRegistry())));

        var result = await tools.PlanVerification(worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False,
                "A broken configuration must fail loudly, never resolve to an empty plan.");
            Assert.That(result.Message, Does.Contain("not valid JSON"));
        });
    }

    [Test]
    public async Task GuardVerification_failsWhenARequiredCheckHasNeverRun()
    {
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .GuardVerification(worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(((VerificationGuardDto)result.Data!).Blocking.Select(check => check.CheckId),
                Is.EquivalentTo(new[] { "architecture", "server" }));
        });
    }

    [Test]
    public async Task RunVerification_thenGuardVerification_satisfiesTheGuard()
    {
        var ledger = new InMemoryReceiptLedgerStore();
        var tools = ToolsOver(Rules(), Changed(ServerSource), ledger, new ScriptedCheckRunner());

        var run = await tools.RunVerification(worktreePath: Worktree);
        var guard = await tools.GuardVerification(worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(run.Succeeded, Is.True);
            Assert.That(guard.Succeeded, Is.True);
            Assert.That(((VerificationGuardDto)guard.Data!).Satisfied, Is.True);
        });
    }

    [Test]
    public async Task GuardVerification_failsAfterTheCodeChangesFollowingASuccessfulRun()
    {
        // Run the checks, then keep editing, then stop: the receipt must not still count.
        var ledger = new InMemoryReceiptLedgerStore();
        await ToolsOver(Rules(), Changed(ServerSource), ledger, new ScriptedCheckRunner())
            .RunVerification(worktreePath: Worktree);

        var edited = new Dictionary<string, string>(StringComparer.Ordinal) { [ServerSource] = "sha256:edited-after-the-run" };
        var guard = await ToolsOver(Rules(), edited, ledger, new ScriptedCheckRunner())
            .GuardVerification(worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(guard.Succeeded, Is.False);
            Assert.That(((VerificationGuardDto)guard.Data!).Blocking.Select(check => check.Status),
                Does.Contain("stale"));
        });
    }

    [Test]
    public async Task GuardVerification_succeedsUnderWarnEnforcementWhileStillReportingTheVerdicts()
    {
        var result = await ToolsOver(
                Rules(VerificationEnforcement.Warn),
                Changed(ServerSource),
                new InMemoryReceiptLedgerStore(),
                new ScriptedCheckRunner())
            .GuardVerification(worktreePath: Worktree);

        var dto = (VerificationGuardDto)result.Data!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, "Warn enforcement must not fail the tool call.");
            Assert.That(dto.Satisfied, Is.False);
            Assert.That(dto.Blocking, Is.Not.Empty, "The verdicts stay visible even under warn.");
        });
    }

    [Test]
    public async Task GuardVerification_failsWhenAChangedFileMatchesNoPathRule()
    {
        var result = await ToolsOver(Rules(), Changed(UnmappedSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .GuardVerification(worktreePath: Worktree);

        Assert.That(((VerificationGuardDto)result.Data!).UnmatchedFiles, Is.EqualTo(new[] { UnmappedSource }));
    }

    [Test]
    public async Task RunVerification_reportsTheFailingCheckAndStopsThere()
    {
        var exitCodes = new Dictionary<string, int>(StringComparer.Ordinal) { ["architecture"] = 1 };
        var runner = new ScriptedCheckRunner(exitCodes);

        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), runner)
            .RunVerification(worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("architecture"));
            Assert.That(runner.Executed, Is.EqualTo(new[] { "architecture" }));
        });
    }

    [Test]
    public async Task RunVerificationCheck_rejectsACheckTheChangesDoNotRequire()
    {
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .RunVerificationCheck("mobile", worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("not required"));
        });
    }

    [Test]
    public async Task RunVerificationCheck_runsAndRecordsOnlyTheNamedCheck()
    {
        var runner = new ScriptedCheckRunner();
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), runner)
            .RunVerificationCheck("server", worktreePath: Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(runner.Executed, Is.EqualTo(new[] { "server" }));
        });
    }

    [Test]
    public async Task RunVerification_saysThereIsNothingToRunWhenNothingChanged()
    {
        var result = await ToolsOver(Rules(), Changed(), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .RunVerification(worktreePath: Worktree);

        Assert.That(result.Message, Does.Contain("Nothing to run"));
    }

    [Test]
    public async Task PlanVerification_plansForARegisteredWorkspaceNamedById()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace().At(Worktree).Build());

        var result = await ToolsOver(
                Rules(),
                Changed(ServerSource),
                new InMemoryReceiptLedgerStore(),
                new ScriptedCheckRunner(),
                registry)
            .PlanVerification(workspaceId: workspace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(((VerificationPlanDto)result.Data!).Checks, Is.Not.Empty);
        });
    }

    [Test]
    public async Task PlanVerification_refusesAWorkspaceIdAndAWorktreePathTogether()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace().At(Worktree).Build());

        var result = await ToolsOver(
                Rules(),
                Changed(ServerSource),
                new InMemoryReceiptLedgerStore(),
                new ScriptedCheckRunner(),
                registry)
            .PlanVerification(workspace.Id, Worktree);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("not both"));
        });
    }

    [Test]
    public async Task PlanVerification_namesAnUnregisteredWorkspaceRatherThanAMissingPath()
    {
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .PlanVerification(workspaceId: "ws-missing");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("ws-missing"));
            Assert.That(result.Message, Does.Contain("not registered"));
        });
    }
}
