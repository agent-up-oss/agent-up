using AgentUp.Server.Features.Verification.Services;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Server.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerificationReportServiceTests
{
    private static CheckVerdict Verdict(string checkId, CheckVerdictKind kind, string detail = "detail")
        => new(checkId, "dotnet test " + checkId, kind, detail);

    [Test]
    public void Summarize_saysNothingIsRequiredWhenNothingChanged()
    {
        var summary = new VerificationReportService().Summarize(GuardReport.Clean);

        Assert.That(summary, Does.Contain("Nothing changed"));
    }

    [Test]
    public void Summarize_namesEveryUnprovenCheckSoAnAgentCanActWithoutOtherOutput()
    {
        var report = new GuardReport(
            [Verdict("server", CheckVerdictKind.NeverRun), Verdict("cli", CheckVerdictKind.Stale)],
            VerificationEnforcement.Block,
            [],
            3);

        var summary = new VerificationReportService().Summarize(report);

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Contain("server"));
            Assert.That(summary, Does.Contain("cli"));
            Assert.That(summary, Does.Contain("run_verification"));
        });
    }

    [Test]
    public void Summarize_callsOutAnIncompleteMapSeparatelyFromUnprovenChecks()
    {
        var report = new GuardReport([], VerificationEnforcement.Block, ["Experiments/scratch/A.cs"], 1);

        Assert.That(new VerificationReportService().Summarize(report), Does.Contain("no path rule"));
    }

    [Test]
    public void Summarize_reportsSkippedPlatformChecksOnAnOtherwiseSatisfiedRun()
    {
        var report = new GuardReport(
            [Verdict("macos-smoke", CheckVerdictKind.Skipped), Verdict("server", CheckVerdictKind.Satisfied)],
            VerificationEnforcement.Block,
            [],
            2);

        var summary = new VerificationReportService().Summarize(report);

        Assert.Multiple(() =>
        {
            Assert.That(summary, Does.Contain("proven"));
            Assert.That(summary, Does.Contain("still required in CI"));
        });
    }

    [Test]
    public void Summarize_distinguishesWarnEnforcementFromBlocking()
    {
        var report = new GuardReport(
            [Verdict("server", CheckVerdictKind.NeverRun)],
            VerificationEnforcement.Warn,
            [],
            1);

        Assert.That(new VerificationReportService().Summarize(report), Does.Contain("warn"));
    }

    [Test]
    public void ToGuardDto_translatesVerdictKindsToStableWireNames()
    {
        var report = new GuardReport(
            [
                Verdict("server", CheckVerdictKind.NeverRun),
                Verdict("macos-smoke", CheckVerdictKind.Skipped),
                Verdict("cli", CheckVerdictKind.Satisfied)
            ],
            VerificationEnforcement.Block,
            [],
            2);

        var dto = new VerificationReportService().ToGuardDto(report, VerificationPlan.Nothing);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Blocking.Single().Status, Is.EqualTo("neverRun"));
            Assert.That(dto.Skipped.Single().Status, Is.EqualTo("skipped"));
            Assert.That(dto.SatisfiedChecks.Single().Status, Is.EqualTo("satisfied"));
            Assert.That(dto.Enforcement, Is.EqualTo("block"));
        });
    }

    [Test]
    public void ToRunDto_reportsFailureWhenAnyCheckExitedNonZero()
    {
        var outcomes = new[]
        {
            new CheckOutcome("architecture", "dotnet test Arch", 0, 10, "ok"),
            new CheckOutcome("server", "dotnet test Server", 1, 20, "boom")
        };

        Assert.That(new VerificationReportService().ToRunDto(outcomes).Succeeded, Is.False);
    }
}
