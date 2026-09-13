using AgentUp.CLI.Features.Verification.DTOs;
using AgentUp.CLI.Features.Verification.Services;
using AgentUp.Verification.Features.Coverage.Models;
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
        var definition = new CheckDefinition("macos-smoke", "./smoke.sh", null, CheckTier.Platform, ["macos"], false, 0, []);
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

    [Test]
    public void WriteCoverage_reportsThePercentageAndTheUncoveredLines()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var coverage = new PatchCoverageResult(
            8, 10, 90d,
            [new UncoveredFile("AgentUp.Server/Features/Git/Services/GitChangesService.cs", [11, 12])],
            []);

        var code = new VerifyOutputService(output, error).WriteCoverage(new VerifyCoverageResult(coverage, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("80%").And.Contain("8/10"));
            Assert.That(output.ToString(), Does.Contain("11-12"));
            Assert.That(error.ToString(), Does.Contain("below the required 90%"));
        });
    }

    [Test]
    public void WriteCoverage_succeedsWhenTheMinimumIsMet()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var coverage = new PatchCoverageResult(10, 10, 90d, [], []);

        var code = new VerifyOutputService(output, error).WriteCoverage(new VerifyCoverageResult(coverage, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public void WriteCoverage_saysTheGateDoesNotApplyWhenNothingIsCoverable()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = new VerifyOutputService(output, error)
            .WriteCoverage(new VerifyCoverageResult(PatchCoverageResult.NothingToCover(90d), null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("does not apply"));
        });
    }

    [Test]
    public void WriteCoverage_tellsTheCallerToCollectCoverageWhenAProjectHasNoReport()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var coverage = new PatchCoverageResult(0, 0, 90d, [], ["AgentUp.Server/Features/X/Services/Y.cs"]);

        var code = new VerifyOutputService(output, error).WriteCoverage(new VerifyCoverageResult(coverage, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("no coverage report"));
            Assert.That(error.ToString(), Does.Contain("AgentUp.Server/Features/X/Services/Y.cs"));
        });
    }

    [Test]
    public void WriteCoverage_rendersAConfigurationErrorInsteadOfPassing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = new VerifyOutputService(output, error)
            .WriteCoverage(new VerifyCoverageResult(null, "'coverage.minimum' is required."));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("minimum"));
        });
    }

    private static SliceCoverage Slice(string name, int covered, int coverable)
        => new(name, covered, coverable);

    [Test]
    public void WriteSliceCoverage_listsEverySliceWithItsCoverage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = new SliceCoverageResult(
            [Slice("AgentUp.Server/Features/Ports", 5, 10), Slice("AgentUp.Server/Features/Git", 9, 10)],
            70d,
            [],
            []);

        var code = new VerifyOutputService(output, error)
            .WriteSliceCoverage(new VerifySliceCoverageResult(result, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("2 feature slice(s)").And.Contain("floor 70%"));
            Assert.That(output.ToString(),
                Does.Contain("AgentUp.Server/Features/Ports").And.Contain("AgentUp.Server/Features/Git"));
            Assert.That(output.ToString(), Does.Contain("5/10").And.Contain("9/10"));
        });
    }

    [Test]
    public void WriteSliceCoverage_marksTheSlicesBelowTheFloor()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = new SliceCoverageResult(
            [Slice("AgentUp.Server/Features/Ports", 5, 10), Slice("AgentUp.Server/Features/Git", 9, 10)],
            70d,
            [],
            []);

        new VerifyOutputService(output, error)
            .WriteSliceCoverage(new VerifySliceCoverageResult(result, null));

        var lines = output.ToString().Split(Environment.NewLine);

        Assert.Multiple(() =>
        {
            Assert.That(lines.Single(line => line.Contains("Ports", StringComparison.Ordinal)),
                Does.Contain("below floor"));
            Assert.That(lines.Single(line => line.Contains("Git", StringComparison.Ordinal)),
                Does.Not.Contain("below floor"));
        });
    }

    [Test]
    public void WriteSliceCoverage_failsAndNamesEachSliceUnderTheFloor()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var failing = Slice("AgentUp.Server/Features/Ports", 5, 10);
        var result = new SliceCoverageResult([failing], 70d, [failing], []);

        var code = new VerifyOutputService(output, error)
            .WriteSliceCoverage(new VerifySliceCoverageResult(result, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(),
                Does.Contain("AgentUp.Server/Features/Ports").And.Contain("50%").And.Contain("70%"));
        });
    }

    [Test]
    public void WriteSliceCoverage_failsAndSaysWhichExemptionToDelete()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = new SliceCoverageResult(
            [Slice("AgentUp.Server/Features/Ports", 9, 10)],
            70d,
            [],
            ["AgentUp.Server/Features/Ports"]);

        var code = new VerifyOutputService(output, error)
            .WriteSliceCoverage(new VerifySliceCoverageResult(result, null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(),
                Does.Contain("AgentUp.Server/Features/Ports").And.Contain("sliceExemptions"));
        });
    }

    [Test]
    public void WriteSliceCoverage_tellsTheCallerToCollectCoverageWhenNoSliceIsMeasured()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = new VerifyOutputService(output, error)
            .WriteSliceCoverage(new VerifySliceCoverageResult(SliceCoverageResult.NothingMeasured(70d), null));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("coverage collection"));
        });
    }

    [Test]
    public void WriteSliceCoverage_rendersAConfigurationErrorInsteadOfPassing()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = new VerifyOutputService(output, error)
            .WriteSliceCoverage(new VerifySliceCoverageResult(null, "'coverage.sliceMinimum' must be a number."));

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("sliceMinimum"));
        });
    }
}
