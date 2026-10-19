using AgentUp.Verification.Features.Coverage.Controllers;
using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Providers;
using AgentUp.Verification.Features.Coverage.Services;
using AgentUp.Verification.Features.Verification.DTOs;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class AuCheckCommandServiceTests
{
    [Test]
    public async Task ExecuteAsync_exitsCannotRunWhenAToolchainIsMissingAndDoesNotCallItAFailure()
    {
        var runner = new RecordingCheckRunner();
        var reports = new RecordingReportWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new VerificationConfigurationBuilder()
            .WithCheck(new CheckBuilder("image").WithCommand("docker info").WithRequires(new ToolchainRequirement("docker", null)))
            .WithCheck(new CheckBuilder("unit").WithCommand("dotnet test"))
            .WithPathRule("packaging/**", "image", "unit")
            .Build();
        var service = Service(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            runner,
            reports,
            new Dictionary<string, ToolchainObservation>(StringComparer.Ordinal)
            {
                ["docker"] = ToolchainObservation.Missing
            },
            output,
            error);

        var code = await service.ExecuteAsync(Invocation(report: "/tmp/report"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.CannotRun));
            Assert.That(runner.Executed, Is.EqualTo(new[] { "unit" }));
            Assert.That(reports.Report!.CouldNotRun, Is.EqualTo(1));
            Assert.That(reports.Report.Checks.Single(check => check.Id == "image").MissingToolchain, Does.Contain("docker"));
            Assert.That(reports.Report.ExitCode, Is.EqualTo(AuCheckExit.CannotRun));
            Assert.That(error.ToString(), Does.Not.Contain("failed"));
        });
    }

    [Test]
    public async Task ExecuteAsync_keepsTheExitCodeWhenTheReportCaptureFails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new VerificationConfigurationBuilder()
            .WithCheck(new CheckBuilder("unit").WithCommand("dotnet test"))
            .WithPathRule("packaging/**", "unit")
            .Build();
        var service = Service(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            new RecordingCheckRunner(),
            new ThrowingReportWriter(),
            null,
            output,
            error);

        var code = await service.ExecuteAsync(Invocation(report: "/tmp/report"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(error.ToString(), Does.Contain("no chromium"));
        });
    }

    [Test]
    public async Task ExecuteAsync_printsHelpAndRejectsABrokenInvocationBeforeLoadingRules()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = Service(UnitConfiguration(), ChangeSetBuilder.Changing().Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, output, error);

        var help = await service.ExecuteAsync(Invocation(command: "help"), CancellationToken.None);
        var invalid = await service.ExecuteAsync(Invocation(error: "Missing value for --repo."), CancellationToken.None);
        var unknown = await service.ExecuteAsync(Invocation(command: "explode"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(help, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(output.ToString(), Does.Contain("Exit codes:"));
            Assert.That(invalid, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(error.ToString(), Does.Contain("Missing value"));
            Assert.That(unknown, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(error.ToString(), Does.Contain("Unknown command 'explode'"));
        });
    }

    [Test]
    public async Task ExecuteAsync_plansAnEmptyTreeAMissingToolchainAndAnIncompleteMap()
    {
        using var emptyOut = new StringWriter();
        using var emptyErr = new StringWriter();
        var empty = Service(UnitConfiguration(), ChangeSetBuilder.Changing().Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, emptyOut, emptyErr);
        var emptyCode = await empty.ExecuteAsync(Invocation(command: "plan"), CancellationToken.None);

        using var toolOut = new StringWriter();
        using var toolErr = new StringWriter();
        var tool = Service(
            DockerConfiguration(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            new Dictionary<string, ToolchainObservation>(StringComparer.Ordinal) { ["docker"] = ToolchainObservation.Missing },
            toolOut,
            toolErr);
        var toolCode = await tool.ExecuteAsync(Invocation(command: "plan"), CancellationToken.None);

        using var mapOut = new StringWriter();
        using var mapErr = new StringWriter();
        var map = Service(UnitConfiguration(), ChangeSetBuilder.Changing(VerificationDomain.UnmappedSource).Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, mapOut, mapErr);
        var mapCode = await map.ExecuteAsync(Invocation(command: "plan"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(emptyCode, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(emptyOut.ToString(), Does.Contain("Nothing changed"));
            Assert.That(toolCode, Is.EqualTo(AuCheckExit.CannotRun));
            Assert.That(toolOut.ToString(), Does.Contain("docker is not installed"));
            Assert.That(mapCode, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(mapOut.ToString(), Does.Contain("verification map is incomplete"));
            Assert.That(mapOut.ToString(), Does.Contain(VerificationDomain.UnmappedSource));
        });
    }

    [Test]
    public async Task ExecuteAsync_recordsASkippedCheckAndOneThatWasNotRun()
    {
        var reports = new RecordingReportWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new VerificationConfigurationBuilder()
            .WithCheck(new CheckBuilder("slow").WithTier(CheckTier.Slow).WithCommand("dotnet test"))
            .WithCheck(new CheckBuilder("hosted").WithTier(CheckTier.Platform).WithPlatforms("windows").WithCommand("dotnet test"))
            .WithCheck(new CheckBuilder("ci").WithCiOnly().WithCommand("dotnet test"))
            .WithPathRule("packaging/**", "slow", "hosted", "ci")
            .Build();
        var service = Service(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            new RecordingCheckRunner(),
            reports,
            null,
            output,
            error);

        await service.ExecuteAsync(Invocation(command: "plan", report: "/tmp/report"), CancellationToken.None);

        var checks = reports.Report!.Checks;
        Assert.Multiple(() =>
        {
            Assert.That(checks.Single(check => check.Id == "slow").Outcome, Is.EqualTo("notRun"));
            Assert.That(checks.Single(check => check.Id == "slow").Tier, Is.EqualTo("slow"));
            Assert.That(checks.Single(check => check.Id == "hosted").Tier, Is.EqualTo("platform"));
            Assert.That(checks.Single(check => check.Id == "hosted").SkipReason, Is.EqualTo("platformMismatch"));
            Assert.That(checks.Single(check => check.Id == "ci").SkipReason, Is.EqualTo("ciOnly"));
            Assert.That(output.ToString(), Does.Contain("Report written to /tmp/report"));
        });
    }

    [Test]
    public async Task ExecuteAsync_failsTheRunWhenACheckExitsNonZero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            new RecordingCheckRunner(new Dictionary<string, int>(StringComparer.Ordinal) { ["unit"] = 7 }),
            new RecordingReportWriter(),
            null,
            output,
            error);

        var code = await service.ExecuteAsync(Invocation(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(output.ToString(), Does.Contain("FAIL unit"));
            Assert.That(error.ToString(), Does.Contain("exit code 7"));
            Assert.That(error.ToString(), Does.Contain("output"));
        });
    }

    [Test]
    public async Task ExecuteAsync_passesWhenEveryRequiredCheckSucceeds()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = Service(UnitConfiguration(), ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, output, error);

        var code = await service.ExecuteAsync(Invocation(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(output.ToString(), Does.Contain("PASS unit"));
            Assert.That(output.ToString(), Does.Contain("passed and were recorded"));
        });
    }

    [Test]
    public async Task ExecuteAsync_runsNothingForAnUnknownOrNonRunnableCheckId()
    {
        var unknownRunner = new RecordingCheckRunner();
        using var unknownOut = new StringWriter();
        using var unknownErr = new StringWriter();
        var unknown = Service(UnitConfiguration(), ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(), unknownRunner, new RecordingReportWriter(), null, unknownOut, unknownErr);
        var unknownCode = await unknown.ExecuteAsync(Invocation(checkId: "missing"), CancellationToken.None);

        var skippedRunner = new RecordingCheckRunner();
        using var skippedOut = new StringWriter();
        using var skippedErr = new StringWriter();
        var skipped = Service(
            DockerConfiguration(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            skippedRunner,
            new RecordingReportWriter(),
            new Dictionary<string, ToolchainObservation>(StringComparer.Ordinal) { ["docker"] = ToolchainObservation.Missing },
            skippedOut,
            skippedErr);
        var skippedCode = await skipped.ExecuteAsync(Invocation(checkId: "image"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(unknownCode, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(unknownOut.ToString(), Does.Contain("Nothing to run"));
            Assert.That(unknownRunner.Executed, Is.Empty);
            Assert.That(skippedCode, Is.EqualTo(AuCheckExit.CannotRun));
            Assert.That(skippedRunner.Executed, Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteAsync_guardReportsEmptyWarnBlockAndAFailedRerun()
    {
        using var emptyOut = new StringWriter();
        using var emptyErr = new StringWriter();
        var empty = Service(UnitConfiguration(), ChangeSetBuilder.Changing().Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, emptyOut, emptyErr);
        var emptyCode = await empty.ExecuteAsync(Invocation(command: "guard"), CancellationToken.None);

        using var warnOut = new StringWriter();
        using var warnErr = new StringWriter();
        var warn = Service(
            new VerificationConfigurationBuilder()
                .WithCheck(new CheckBuilder("unit").WithCommand("dotnet test"))
                .WithPathRule("packaging/**", "unit")
                .WithEnforcement(VerificationEnforcement.Warn)
                .Build(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            null,
            warnOut,
            warnErr);
        var warnCode = await warn.ExecuteAsync(Invocation(command: "guard"), CancellationToken.None);

        using var blockOut = new StringWriter();
        using var blockErr = new StringWriter();
        var block = Service(UnitConfiguration(), ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, blockOut, blockErr);
        var blockCode = await block.ExecuteAsync(Invocation(command: "guard"), CancellationToken.None);

        using var failedOut = new StringWriter();
        using var failedErr = new StringWriter();
        var failed = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            new RecordingCheckRunner(new Dictionary<string, int>(StringComparer.Ordinal) { ["unit"] = 1 }),
            new RecordingReportWriter(),
            null,
            failedOut,
            failedErr);
        var failedCode = await failed.ExecuteAsync(Invocation(command: "guard", runMissing: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(emptyCode, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(emptyOut.ToString(), Does.Contain("Nothing changed"));
            Assert.That(warnCode, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(warnOut.ToString(), Does.Contain("enforcement is warn"));
            Assert.That(blockCode, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(blockErr.ToString(), Does.Contain("au-check run"));
            Assert.That(failedCode, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(failedOut.ToString(), Does.Contain("exited 1"));
        });
    }

    [Test]
    public async Task ExecuteAsync_guardProvesTheTreeAfterRunningWhatIsMissing()
    {
        var reports = new RecordingReportWriter();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new RecordingCheckRunner();
        var service = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build(),
            runner,
            reports,
            null,
            output,
            error);

        var code = await service.ExecuteAsync(Invocation(command: "guard", report: "/tmp/guard", runMissing: true), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(runner.Executed, Is.EqualTo(new[] { "unit" }));
            Assert.That(output.ToString(), Does.Contain("is proven"));
            Assert.That(output.ToString(), Does.Contain("Report written to /tmp/guard"));
            Assert.That(reports.Report!.ExitCode, Is.EqualTo(AuCheckExit.Passed));
        });
    }

    [Test]
    public async Task ExecuteAsync_runFailsAnIncompleteMap()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = Service(UnitConfiguration(), ChangeSetBuilder.Changing(VerificationDomain.UnmappedSource).Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, output, error);

        var code = await service.ExecuteAsync(Invocation(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(error.ToString(), Does.Contain("verification map is incomplete"));
            Assert.That(error.ToString(), Does.Contain(VerificationDomain.UnmappedSource));
        });
    }

    [Test]
    public async Task ExecuteAsync_guardFailsAnIncompleteMap()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = Service(UnitConfiguration(), ChangeSetBuilder.Changing(VerificationDomain.UnmappedSource).Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, output, error);

        var code = await service.ExecuteAsync(Invocation(command: "guard"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(error.ToString(), Does.Contain("verification map is incomplete"));
            Assert.That(error.ToString(), Does.Contain(VerificationDomain.UnmappedSource));
        });
    }

    [Test]
    public async Task ExecuteAsync_rejectsConfigurationThatCannotBeLoaded()
    {
        using var verificationOut = new StringWriter();
        using var verificationErr = new StringWriter();
        var verification = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing().Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            null,
            verificationOut,
            verificationErr,
            loader: new ThrowingVerificationLoader());
        var verificationCode = await verification.ExecuteAsync(Invocation(command: "plan"), CancellationToken.None);

        using var coverageOut = new StringWriter();
        using var coverageErr = new StringWriter();
        var coverage = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing().Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            null,
            coverageOut,
            coverageErr,
            patch: new PatchCoverageController(new PatchCoverageService(
                new ThrowingCoverageLoader(),
                new StaticCoverageReportReader(CoverageReport.Empty),
                [new StaticChangedLineSource(ChangedLines.None)],
                new PathGlobProvider())));
        var coverageCode = await coverage.ExecuteAsync(Invocation(command: "coverage"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(verificationCode, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(verificationErr.ToString(), Does.Contain("broken rules"));
            Assert.That(coverageCode, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(coverageErr.ToString(), Does.Contain("broken coverage"));
        });
    }

    [Test]
    public async Task ExecuteAsync_coveragePassesWhenNothingIsCoverableAndFailsWhenAReportIsMissing()
    {
        using var clearOut = new StringWriter();
        using var clearErr = new StringWriter();
        var clear = Service(UnitConfiguration(), ChangeSetBuilder.Changing().Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, clearOut, clearErr);
        var clearCode = await clear.ExecuteAsync(Invocation(command: "coverage"), CancellationToken.None);

        using var missingOut = new StringWriter();
        using var missingErr = new StringWriter();
        var missing = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing().Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            null,
            missingOut,
            missingErr,
            patch: PatchOver(CoverageDomain.Configuration().Build(), CoverageReport.Empty, ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10).Build()));
        var missingCode = await missing.ExecuteAsync(Invocation(command: "coverage"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(clearCode, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(clearOut.ToString(), Does.Contain("does not apply"));
            Assert.That(missingCode, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(missingErr.ToString(), Does.Contain("no coverage report"));
        });
    }

    [Test]
    public async Task ExecuteAsync_coverageFailsWhenChangedLinesMissTheFloor()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var service = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing().Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            null,
            output,
            error,
            patch: PatchOver(
                CoverageDomain.Configuration().Build(),
                CoverageReport.FromFiles([new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Uncovered(11).Build()]),
                ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11).Build()));

        var code = await service.ExecuteAsync(Invocation(command: "coverage", minimum: 90), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(output.ToString(), Does.Contain("Patch coverage 50%"));
            Assert.That(output.ToString(), Does.Contain("minimum 90%"));
        });
    }

    [Test]
    public async Task ExecuteAsync_slicesFailWhenNothingWasMeasuredOrASliceMissesTheFloor()
    {
        using var emptyOut = new StringWriter();
        using var emptyErr = new StringWriter();
        var empty = Service(UnitConfiguration(), ChangeSetBuilder.Changing().Build(), new RecordingCheckRunner(), new RecordingReportWriter(), null, emptyOut, emptyErr);
        var emptyCode = await empty.ExecuteAsync(Invocation(command: "slices"), CancellationToken.None);

        using var lowOut = new StringWriter();
        using var lowErr = new StringWriter();
        var low = Service(
            UnitConfiguration(),
            ChangeSetBuilder.Changing().Build(),
            new RecordingCheckRunner(),
            new RecordingReportWriter(),
            null,
            lowOut,
            lowErr,
            slice: SliceOver(
                CoverageDomain.Configuration().WithSliceMinimum(90).Build(),
                CoverageReport.FromFiles([new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Uncovered(2, 3, 4).Build()])));
        var lowCode = await low.ExecuteAsync(Invocation(command: "slices"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(emptyCode, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(emptyErr.ToString(), Does.Contain("No coverage report"));
            Assert.That(lowCode, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(lowOut.ToString(), Does.Contain("1 feature slice"));
        });
    }

    private static AuCheckInvocation Invocation(
        string command = "run",
        string? checkId = null,
        string? report = null,
        bool runMissing = false,
        double? minimum = null,
        string? error = null)
        => new(command, checkId, "/repo", "/repo/agent-up.json", false, report, runMissing, minimum, error);

    private static VerificationConfiguration UnitConfiguration()
        => new VerificationConfigurationBuilder()
            .WithCheck(new CheckBuilder("unit").WithCommand("dotnet test"))
            .WithPathRule("packaging/**", "unit")
            .Build();

    private static VerificationConfiguration DockerConfiguration()
        => new VerificationConfigurationBuilder()
            .WithCheck(new CheckBuilder("image").WithCommand("docker info").WithRequires(new ToolchainRequirement("docker", null)))
            .WithPathRule("packaging/**", "image")
            .Build();

    private static PatchCoverageController PatchOver(CoverageConfiguration configuration, CoverageReport report, ChangedLines changed)
        => new(new PatchCoverageService(
            new StubCoverageConfigurationLoader(configuration),
            new StaticCoverageReportReader(report),
            [new StaticChangedLineSource(changed)],
            new PathGlobProvider()));

    private static SliceCoverageController SliceOver(CoverageConfiguration configuration, CoverageReport report)
        => new(new SliceCoverageService(
            new StubCoverageConfigurationLoader(configuration),
            new StaticCoverageReportReader(report),
            new PathGlobProvider()));

    private static AuCheckCommandService Service(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        ICheckRunner runner,
        IRunReportWriter reports,
        IReadOnlyDictionary<string, ToolchainObservation>? toolchains,
        TextWriter output,
        TextWriter error,
        PatchCoverageController? patch = null,
        SliceCoverageController? slice = null,
        IVerificationConfigurationLoader? loader = null)
    {
        var plans = new VerificationPlanService(
            loader ?? new StubConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(VerificationDomain.Linux, toolchains: toolchains)),
            [new StaticChangedContentSource("test", changed)]);
        var ledger = new InMemoryReceiptLedgerStore();
        var globs = new PathGlobProvider();
        return new AuCheckCommandService(
            plans,
            new VerificationRunService(plans, ledger, runner, FakeVerificationClock.AtNoon()),
            new VerificationGuardService(plans, ledger),
            patch ?? new PatchCoverageController(new PatchCoverageService(
                new CoverageConfigurationLoader(),
                new CoverageReportReader(new CoberturaReportParser()),
                [new GitChangedLineSource(new UnifiedDiffParser())],
                globs)),
            slice ?? new SliceCoverageController(new SliceCoverageService(
                new CoverageConfigurationLoader(),
                new CoverageReportReader(new CoberturaReportParser()),
                globs)),
            new FixedRevision(),
            new ContentHashProvider(),
            reports,
            "0.0.0-test",
            output,
            error);
    }

    private sealed class FixedRevision : IGitRevisionProvider
    {
        public string Resolve(string repositoryRoot) => "abc123";
    }

    private sealed class RecordingReportWriter : IRunReportWriter
    {
        public RunReport? Report { get; private set; }

        public Task WriteAsync(string directory, RunReport report, CancellationToken cancellationToken)
        {
            Report = report;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingReportWriter : IRunReportWriter
    {
        public Task WriteAsync(string directory, RunReport report, CancellationToken cancellationToken)
            => throw new ReportCaptureException("no chromium");
    }

    private sealed class ThrowingVerificationLoader : IVerificationConfigurationLoader
    {
        public VerificationConfiguration Load(string repositoryRoot)
            => throw new VerificationConfigurationException("broken rules");
    }

    private sealed class ThrowingCoverageLoader : ICoverageConfigurationLoader
    {
        public CoverageConfiguration Load(string repositoryRoot)
            => throw new CoverageConfigurationException("broken coverage");
    }
}
