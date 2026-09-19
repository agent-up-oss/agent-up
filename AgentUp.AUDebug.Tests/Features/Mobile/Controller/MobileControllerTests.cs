using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Controllers;
using AgentUp.AUDebug.Features.Mobile.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Controller;

[TestFixture]
public sealed class MobileControllerTests
{
    [Test]
    public async Task Screenshot_returnsPath()
    {
        var screenshots = new FakeWebScreenshotDriver();
        var result = await new MobileController(
            new MobileCommandService(screenshots, new FakeMobileSurfaceDriver(), new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.ScreenshotAction)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(screenshots.Captures, Has.Count.EqualTo(1));
        Assert.That(result.ArtifactPath, Is.Not.Null);
    }

    [Test]
    public async Task Login_routesToSurface()
    {
        var surface = new FakeMobileSurfaceDriver();
        var result = await new MobileController(
            new MobileCommandService(new FakeWebScreenshotDriver(), surface, new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.LoginAction)
                .WithPassword(DebugDomain.Password)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(surface.Password, Is.EqualTo("test"));
    }

    [Test]
    public async Task UnknownAction_fails()
    {
        var result = await new MobileController(
            new MobileCommandService(new FakeWebScreenshotDriver(), new FakeMobileSurfaceDriver(), new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing("nope")
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown mobile action"));
    }

    [Test]
    public async Task OpenAgent_routesToService()
    {
        var surface = new FakeMobileSurfaceDriver();
        var result = await new MobileController(
            new MobileCommandService(new FakeWebScreenshotDriver(), surface, new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment())).RunAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.OpenAgentAction)
                .ForWorkspace(DebugDomain.WorkspaceName)
                .WithPassword(DebugDomain.Password)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(surface.CaptureAgentPath, Is.Not.Null);
    }
}
