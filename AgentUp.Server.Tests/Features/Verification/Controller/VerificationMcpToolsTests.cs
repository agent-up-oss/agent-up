using AgentUp.Server.Features.Verification.Controllers;
using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Server.Features.Verification.Services;
using AgentUp.Server.Tests.Fake;
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
        ScriptedCheckRunner runner)
    {
        var plans = new VerificationPlanService(
            new StubVerificationConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider("linux")),
            [new StaticChangedContentSource(changed)]);

        return new VerificationMcpTools(new VerificationMcpService(
            plans,
            new VerificationRunService(plans, ledger, runner, new FakeVerificationClock()),
            new VerificationGuardService(plans, ledger),
            new VerificationReportService()));
    }

    [Test]
    public async Task PlanVerification_rejectsAMissingWorktreePath()
    {
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .PlanVerification("  ", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("worktreePath"));
        });
    }

    [Test]
    public async Task PlanVerification_reportsTheChecksTheStaticRulesRequire()
    {
        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), new ScriptedCheckRunner())
            .PlanVerification(Worktree, CancellationToken.None);

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
            new VerificationReportService()));

        var result = await tools.PlanVerification(Worktree, CancellationToken.None);

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
            .GuardVerification(Worktree, CancellationToken.None);

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

        var run = await tools.RunVerification(Worktree, CancellationToken.None);
        var guard = await tools.GuardVerification(Worktree, CancellationToken.None);

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
            .RunVerification(Worktree, CancellationToken.None);

        var edited = new Dictionary<string, string>(StringComparer.Ordinal) { [ServerSource] = "sha256:edited-after-the-run" };
        var guard = await ToolsOver(Rules(), edited, ledger, new ScriptedCheckRunner())
            .GuardVerification(Worktree, CancellationToken.None);

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
            .GuardVerification(Worktree, CancellationToken.None);

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
            .GuardVerification(Worktree, CancellationToken.None);

        Assert.That(((VerificationGuardDto)result.Data!).UnmatchedFiles, Is.EqualTo(new[] { UnmappedSource }));
    }

    [Test]
    public async Task RunVerification_reportsTheFailingCheckAndStopsThere()
    {
        var exitCodes = new Dictionary<string, int>(StringComparer.Ordinal) { ["architecture"] = 1 };
        var runner = new ScriptedCheckRunner(exitCodes);

        var result = await ToolsOver(Rules(), Changed(ServerSource), new InMemoryReceiptLedgerStore(), runner)
            .RunVerification(Worktree, CancellationToken.None);

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
            .RunVerificationCheck(Worktree, "mobile", CancellationToken.None);

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
            .RunVerificationCheck(Worktree, "server", CancellationToken.None);

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
            .RunVerification(Worktree, CancellationToken.None);

        Assert.That(result.Message, Does.Contain("Nothing to run"));
    }
}
