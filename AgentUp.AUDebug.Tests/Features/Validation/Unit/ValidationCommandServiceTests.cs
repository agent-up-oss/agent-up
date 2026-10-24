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

        var result = await service.RunAsync(DebugDomain.Validate("mobile").Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.Message, Does.Contain("Mobile surface replay is not implemented"));
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
            DebugDomain.Validate()
                .WithFlow("open-git")
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
            DebugDomain.Validate().TimingOutAfter(TimeSpan.FromMilliseconds(20)).Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }
}
