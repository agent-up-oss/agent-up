using AgentUp.Verification.Features.Coverage.Controllers;
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

        var code = await service.ExecuteAsync(Invocation("/tmp/report"), CancellationToken.None);

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

        var code = await service.ExecuteAsync(Invocation("/tmp/report"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(AuCheckExit.Passed));
            Assert.That(error.ToString(), Does.Contain("no chromium"));
        });
    }

    private static AuCheckInvocation Invocation(string? report)
        => new("run", null, "/repo", "/repo/agent-up.json", false, report, false, null, null);

    private static AuCheckCommandService Service(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        ICheckRunner runner,
        IRunReportWriter reports,
        IReadOnlyDictionary<string, ToolchainObservation>? toolchains,
        TextWriter output,
        TextWriter error)
    {
        var plans = new VerificationPlanService(
            new StubConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(VerificationDomain.Linux, toolchains: toolchains)),
            [new StaticChangedContentSource("test", changed)]);
        var globs = new PathGlobProvider();
        return new AuCheckCommandService(
            plans,
            new VerificationRunService(plans, new InMemoryReceiptLedgerStore(), runner, FakeVerificationClock.AtNoon()),
            new VerificationGuardService(plans, new InMemoryReceiptLedgerStore()),
            new PatchCoverageController(new PatchCoverageService(
                new CoverageConfigurationLoader(),
                new CoverageReportReader(new CoberturaReportParser()),
                [new GitChangedLineSource(new UnifiedDiffParser())],
                globs)),
            new SliceCoverageController(new SliceCoverageService(
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
}
