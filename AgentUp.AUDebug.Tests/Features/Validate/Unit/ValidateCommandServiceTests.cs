using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Validate.Services;
using AgentUp.AUDebug.Shared.Providers;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Validate.Unit;

[TestFixture]
public sealed class ValidateCommandServiceTests
{
    [Test]
    public async Task UnknownSurface_doesNotRunTheMobileRunner()
    {
        var runner = new FakeMobileValidationRunner();
        var result = await Service(runner).RunAsync(DebugDomain.Validate("tablet").Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("unknown validation surface"));
            Assert.That(runner.Ran, Is.Empty);
        });
    }

    [Test]
    public async Task DesktopSurface_refusesReplay()
    {
        var runner = new FakeMobileValidationRunner();
        var result = await Service(runner).RunAsync(DebugDomain.Validate("desktop").Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("Desktop surface replay is not implemented."));
            Assert.That(runner.Ran, Is.Empty);
        });
    }

    [Test]
    public async Task MobileSurface_runsTheNamedFlowIntoTheDefaultOutDir()
    {
        var runner = new FakeMobileValidationRunner();
        var result = await Service(runner).RunAsync(
            DebugDomain.Validate().ForFlow("git").Build(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(runner.Ran, Has.Count.EqualTo(1));
            Assert.That(runner.Ran[0].FlowId, Is.EqualTo("git"));
            Assert.That(runner.Ran[0].OutDir, Does.Contain("artifacts/mobile-validation"));
            Assert.That(result.ArtifactPath, Does.Contain("report.json"));
        });
    }

    [Test]
    public async Task Failure_printsStderrWhenStdoutIsEmpty()
    {
        var runner = new FakeMobileValidationRunner
        {
            NextResult = new ProcessResult(1, "", "locator not found")
        };
        var result = await Service(runner).RunAsync(DebugDomain.Validate().Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("locator not found"));
    }

    [Test]
    public async Task Timeout_returnsFailure()
    {
        var runner = new FakeMobileValidationRunner { DelayUntilCanceled = true };
        var result = await Service(runner).RunAsync(
            DebugDomain.Validate().TimingOutAfter(TimeSpan.FromMilliseconds(40)).Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }

    private static ValidateCommandService Service(FakeMobileValidationRunner runner)
        => new(runner, new FakePathValidator("/tmp/au-debug-tests"));
}
