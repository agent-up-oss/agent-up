using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.CLI.Features.Verification.Services;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.CLI.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerifyOutputServiceTests
{
    private static CheckVerdict Verdict(string checkId, CheckVerdictKind kind, string detail = "has not run")
        => new(checkId, "dotnet test " + checkId, kind, detail);

    [Test]
    public void WriteGuard_hookFormatStaysSilentWhenEverythingIsProven()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var report = new GuardReport([Verdict("server", CheckVerdictKind.Satisfied)], VerificationEnforcement.Block, [], 1);

        var code = new VerifyOutputService(output, error).WriteGuard(new VerifyGuardResult(report, null), VerifyOutputFormat.Hook);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Is.Empty, "A guard that prints on success trains everyone to ignore it.");
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public void WriteGuard_hookFormatReturnsTwoAndNamesBlockersOnStandardError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var report = new GuardReport(
            [Verdict("server", CheckVerdictKind.NeverRun), Verdict("cli", CheckVerdictKind.Stale, "bytes changed")],
            VerificationEnforcement.Block,
            [],
            2);

        var code = new VerifyOutputService(output, error).WriteGuard(new VerifyGuardResult(report, null), VerifyOutputFormat.Hook);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(2), "The Stop hook relies on exit 2 to feed the text back.");
            Assert.That(error.ToString(), Does.Contain("server").And.Contain("cli"));
            Assert.That(error.ToString(), Does.Contain("run_verification"));
        });
    }

    [Test]
    public void WriteGuard_hookFormatStaysSilentUnderWarnEnforcement()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var report = new GuardReport([Verdict("server", CheckVerdictKind.NeverRun)], VerificationEnforcement.Warn, [], 1);

        var code = new VerifyOutputService(output, error).WriteGuard(new VerifyGuardResult(report, null), VerifyOutputFormat.Hook);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public void WriteGuard_textFormatReportsWarnEnforcementWithoutFailing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var report = new GuardReport([Verdict("server", CheckVerdictKind.NeverRun)], VerificationEnforcement.Warn, [], 1);

        var code = new VerifyOutputService(output, error).WriteGuard(new VerifyGuardResult(report, null), VerifyOutputFormat.Text);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("enforcement is warn"));
        });
    }

    [Test]
    public void WriteGuard_listsFilesThatMatchedNoRule()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var report = new GuardReport([], VerificationEnforcement.Block, ["Experiments/scratch/A.cs"], 1);

        new VerifyOutputService(output, error).WriteGuard(new VerifyGuardResult(report, null), VerifyOutputFormat.Text);

        Assert.That(output.ToString(), Does.Contain("Experiments/scratch/A.cs").And.Contain("incomplete"));
    }

    [Test]
    public void WriteRun_returnsOneAndWritesTheFailingOutputToStandardError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var outcomes = new[] { new CheckOutcome("server", "dotnet test Server", 1, 30, "assertion failed here") };

        var code = new VerifyOutputService(output, error).WriteRun(new VerifyRunResult(outcomes, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("assertion failed here"));
        });
    }

    [Test]
    public void WriteRun_saysThereIsNothingToRunForAnEmptyPlan()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = new VerifyOutputService(output, error).WriteRun(new VerifyRunResult([], null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("Nothing to run"));
        });
    }

    [Test]
    public void WritePlan_marksAPlatformCheckAsStillRequiredInCi()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var definition = new CheckDefinition("macos-smoke", "./smoke.sh", null, CheckTier.Platform, ["macos"], false, []);
        var plan = new VerificationPlan(
            [new PlannedCheck(definition, new Dictionary<string, string>(StringComparer.Ordinal), CheckSkipReason.PlatformMismatch, ["packaging/**"])],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["packaging/x"] = "sha256:1" },
            []);

        new VerifyOutputService(output, error).WritePlan(new VerifyPlanResult(plan, null));

        Assert.That(output.ToString(), Does.Contain("still required in CI"));
    }

    [Test]
    public void WriteGuard_rendersAConfigurationErrorInsteadOfPassingVacuously()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = new VerifyOutputService(output, error)
            .WriteGuard(new VerifyGuardResult(null, "agent-up.json is not valid JSON"), VerifyOutputFormat.Hook);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("not valid JSON"));
        });
    }
}
