using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Services;
using AgentUp.AUDebug.Tests.Fake;
using AgentUp.AUDebug.Tests.Support;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Unit;

[TestFixture]
public sealed class MobileCommandServiceTests
{
    [Test]
    public async Task Login_requiresPassword()
    {
        var environment = new FakeEnvironment { AdminPassword = null };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            new FakeMobileSurfaceDriver(),
            new FakeWorkspaceClient(),
            new FakeSessionStore(),
            environment).LoginAsync(Command(password: null), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task Login_drivesSurfaceThenScreenshots()
    {
        var surface = new FakeMobileSurfaceDriver();
        var shots = new FakeWebScreenshotDriver();
        var result = await new MobileCommandService(shots, surface, new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment())
            .LoginAsync(Command(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(surface.ServerUrl, Is.EqualTo(DebugLayout.ServerUrl));
            Assert.That(surface.Password, Is.EqualTo("test"));
            Assert.That(shots.Captures, Has.Count.EqualTo(1));
            Assert.That(shots.Captures[0].Url, Is.EqualTo($"{DebugLayout.MobileUrl}/"));
            Assert.That(shots.Captures[0].UserDataDirectory, Is.EqualTo(surface.UserDataDirectory));
        });
    }

    [Test]
    public async Task Screenshot_usesLoginProfileAndRootUrl()
    {
        var surface = new FakeMobileSurfaceDriver();
        var shots = new FakeWebScreenshotDriver();
        var result = await new MobileCommandService(shots, surface, new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment())
            .ScreenshotAsync(DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.ScreenshotAction)
                .Build(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(shots.Captures[0].Url, Is.EqualTo($"{DebugLayout.MobileUrl}/"));
            Assert.That(shots.Captures[0].UserDataDirectory, Is.EqualTo(surface.UserDataDirectory));
        });
    }

    [Test]
    public async Task Login_timeout_returnsFailure()
    {
        var surface = new FakeMobileSurfaceDriver { DelayUntilCanceled = true };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            surface,
            new FakeWorkspaceClient(),
            new FakeSessionStore(),
            new FakeEnvironment()).LoginAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.LoginAction)
                .WithPassword(DebugDomain.Password)
                .TimingOutAfter(TimeSpan.FromMilliseconds(30))
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }

    [Test]
    public async Task Screenshot_mapsDriverErrors()
    {
        var shots = new FakeWebScreenshotDriver { CaptureException = new InvalidOperationException("cdp failed") };
        var result = await new MobileCommandService(
            shots,
            new FakeMobileSurfaceDriver(),
            new FakeWorkspaceClient(),
            new FakeSessionStore(),
            new FakeEnvironment()).ScreenshotAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.ScreenshotAction)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Is.EqualTo("cdp failed"));
    }

    [Test]
    public async Task Login_mapsHttpFailures()
    {
        var surface = new FakeMobileSurfaceDriver { LoginException = new HttpRequestException("refused") };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            surface,
            new FakeWorkspaceClient(),
            new FakeSessionStore(),
            new FakeEnvironment()).LoginAsync(Command(), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Is.EqualTo("refused"));
    }

    [Test]
    public async Task OpenAgent_mapsMalformedWorkspaceUrls()
    {
        var workspaces = new FakeWorkspaceClient { Error = new UriFormatException("bad workspace url") };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            new FakeMobileSurfaceDriver(),
            workspaces,
            new FakeSessionStore(),
            new FakeEnvironment()).OpenAgentAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.OpenAgentAction)
                .ForWorkspace(DebugDomain.WorkspaceName)
                .WithPassword(DebugDomain.Password)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Is.EqualTo("bad workspace url"));
    }

    [Test]
    public async Task OpenAgent_screenshotsWorkspaceAgentRoute()
    {
        var surface = new FakeMobileSurfaceDriver();
        var workspaces = new FakeWorkspaceClient { Id = "ws-9" };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            surface,
            workspaces,
            new FakeSessionStore(),
            new FakeEnvironment()).OpenAgentAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.OpenAgentAction)
                .ForWorkspace(DebugDomain.WorkspaceName)
                .WithPassword(DebugDomain.Password)
                .Build(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(workspaces.Name, Is.EqualTo("Agent-Up"));
            Assert.That(surface.CaptureAgentPath, Is.Not.Null);
            Assert.That(result.ArtifactPath, Is.EqualTo(surface.CaptureAgentPath));
        });
    }

    [Test]
    public async Task OpenAgent_httpClientTimeout_returnsFailure()
    {
        var workspaces = new FakeWorkspaceClient { Error = new TaskCanceledException() };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            new FakeMobileSurfaceDriver(),
            workspaces,
            new FakeSessionStore(),
            new FakeEnvironment()).OpenAgentAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.OpenAgentAction)
                .ForWorkspace(DebugDomain.WorkspaceName)
                .WithPassword(DebugDomain.Password)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out talking to"));
    }

    [Test]
    public async Task OpenAgent_requiresPassword()
    {
        var environment = new FakeEnvironment { AdminPassword = null };
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            new FakeMobileSurfaceDriver(),
            new FakeWorkspaceClient(),
            new FakeSessionStore(),
            environment).OpenAgentAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.OpenAgentAction)
                .ForWorkspace(DebugDomain.WorkspaceName)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("AGENTUP_ADMIN_PASSWORD"));
    }

    [Test]
    public async Task OpenAgent_requiresWorkspaceName()
    {
        var result = await new MobileCommandService(
            new FakeWebScreenshotDriver(),
            new FakeMobileSurfaceDriver(),
            new FakeWorkspaceClient(),
            new FakeSessionStore(),
            new FakeEnvironment()).OpenAgentAsync(
            DebugDomain.Command(DebugDomain.MobileSurface)
                .Doing(DebugDomain.OpenAgentAction)
                .WithPassword(DebugDomain.Password)
                .Build(),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("requires a workspace name"));
    }

    private static DebugCommandDto Command(string? password = "test")
        => new("mobile", "mobile", "login", null, password, TimeSpan.FromSeconds(30), false);
}
