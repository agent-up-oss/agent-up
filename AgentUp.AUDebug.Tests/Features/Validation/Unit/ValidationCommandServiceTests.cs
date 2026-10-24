using AgentUp.AUDebug.Features.Validation.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Validation.Unit;

[TestFixture]
public sealed class ValidationCommandServiceTests
{
    [Test]
    public async Task RunAsync_refusesANonDesktopSurface()
    {
        var launch = new FakeDesktopValidationLaunch();
        var service = new ValidationCommandService(launch, new FakeEnvironment());

        var result = await service.RunAsync(DebugDomain.Validate(DebugDomain.MobileSurface).Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("--surface must be desktop"));
            Assert.That(launch.Ran, Is.Empty);
        });
    }

    [Test]
    public async Task RunAsync_passesFlowWorkspaceAndPasswordToDesktop()
    {
        var launch = new FakeDesktopValidationLaunch();
        var environment = new FakeEnvironment { AdminPassword = "from-env" };
        var service = new ValidationCommandService(launch, environment);

        var result = await service.RunAsync(
            DebugDomain.Validate(DebugDomain.DesktopSurface)
                .ForFlow("open-git")
                .ForWorkspace("ws-1")
                .WritingTo("/tmp/qa")
                .Build(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(launch.Ran[0].FlowId, Is.EqualTo("open-git"));
            Assert.That(launch.Ran[0].WorkspaceId, Is.EqualTo("ws-1"));
            Assert.That(launch.Ran[0].Password, Is.EqualTo("from-env"));
            Assert.That(result.ArtifactPath, Is.EqualTo("/tmp/qa"));
        });
    }

    [Test]
    public async Task RunAsync_timesOutWhenDesktopNeverExits()
    {
        var launch = new FakeDesktopValidationLaunch { DelayUntilCanceled = true };
        var service = new ValidationCommandService(launch, new FakeEnvironment());

        var result = await service.RunAsync(
            DebugDomain.Validate(DebugDomain.DesktopSurface).TimingOutAfter(TimeSpan.FromMilliseconds(20)).Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }

    [Test]
    public async Task RunAsync_joinsStdoutAndStderrWhenBothArePresent()
    {
        var launch = new FakeDesktopValidationLaunch
        {
            NextResult = new(0, "ok\n", "warn\n")
        };
        var service = new ValidationCommandService(launch, new FakeEnvironment());

        var result = await service.RunAsync(DebugDomain.Validate(DebugDomain.DesktopSurface).Build(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Message, Does.Contain("ok"));
        Assert.That(result.Message, Does.Contain("warn"));
    }

    [Test]
    public async Task RunAsync_usesArtifactFallbackWhenDesktopPrintsNothing()
    {
        var launch = new FakeDesktopValidationLaunch
        {
            NextResult = new(1, "  ", "")
        };
        var service = new ValidationCommandService(launch, new FakeEnvironment());

        var failed = await service.RunAsync(DebugDomain.Validate(DebugDomain.DesktopSurface).WritingTo("/tmp/qa").Build(), CancellationToken.None);
        launch.NextResult = new(0, "", "  ");
        var passed = await service.RunAsync(DebugDomain.Validate(DebugDomain.DesktopSurface).WritingTo("/tmp/qa").Build(), CancellationToken.None);

        Assert.That(failed.Message, Does.Contain("Desktop validation failed. Artifacts: /tmp/qa"));
        Assert.That(passed.Message, Does.Contain("Desktop validation passed. Artifacts: /tmp/qa"));
    }

    [Test]
    public async Task RunAsync_surfacesInvalidOperationAndHttpFailures()
    {
        var launch = new FakeDesktopValidationLaunch { ThrowOnRun = new InvalidOperationException("no desktop") };
        var service = new ValidationCommandService(launch, new FakeEnvironment());

        var invalid = await service.RunAsync(DebugDomain.Validate(DebugDomain.DesktopSurface).Build(), CancellationToken.None);
        launch.ThrowOnRun = new HttpRequestException("server down");
        var http = await service.RunAsync(DebugDomain.Validate(DebugDomain.DesktopSurface).Build(), CancellationToken.None);

        Assert.That(invalid.ExitCode, Is.EqualTo(1));
        Assert.That(invalid.Message, Is.EqualTo("no desktop"));
        Assert.That(http.Message, Is.EqualTo("server down"));
    }
}
