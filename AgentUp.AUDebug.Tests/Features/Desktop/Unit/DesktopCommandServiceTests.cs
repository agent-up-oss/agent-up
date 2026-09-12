using AgentUp.AUDebug.Features.Desktop.Services;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Desktop.Unit;

[TestFixture]
public sealed class DesktopCommandServiceTests
{
    [Test]
    public async Task Login_requiresPassword()
    {
        var environment = new FakeEnvironment { AdminPassword = null };
        var result = await Service(new FakeDesktopWindowDriver(), new FakeWorkspaceClient(), environment)
            .LoginAsync(Command("login", password: null), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("AGENTUP_ADMIN_PASSWORD"));
    }

    [Test]
    public async Task Login_typesPasswordAndScreenshots()
    {
        var windows = new FakeDesktopWindowDriver();
        var result = await Service(windows, new FakeWorkspaceClient(), new FakeEnvironment())
            .LoginAsync(Command("login"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(windows.LastPassword, Is.EqualTo("test"));
            Assert.That(windows.Captures, Is.EqualTo(1));
            Assert.That(result.ArtifactPath, Is.Not.Null);
        });
    }

    [Test]
    public async Task StartWorkspace_startsNamedWorkspace()
    {
        var client = new FakeWorkspaceClient();
        var result = await Service(new FakeDesktopWindowDriver(), client, new FakeEnvironment())
            .StartWorkspaceAsync(Command("start-workspace"), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(client.Name, Is.EqualTo("Agent-Up"));
    }

    [Test]
    public async Task Screenshot_timeout_returnsFailure()
    {
        var windows = new FakeDesktopWindowDriver { DelayUntilCanceled = true };
        var result = await Service(windows, new FakeWorkspaceClient(), new FakeEnvironment())
            .ScreenshotAsync(Command("screenshot", timeout: TimeSpan.FromMilliseconds(30)), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("Timed out"));
    }

    [Test]
    public async Task Screenshot_mapsDriverErrors()
    {
        var windows = new FakeDesktopWindowDriver { CaptureException = new InvalidOperationException("no window") };
        var result = await Service(windows, new FakeWorkspaceClient(), new FakeEnvironment())
            .ScreenshotAsync(Command("screenshot"), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Is.EqualTo("no window"));
    }

    [Test]
    public async Task StartWorkspace_requiresPassword()
    {
        var result = await Service(new FakeDesktopWindowDriver(), new FakeWorkspaceClient(), new FakeEnvironment { AdminPassword = null })
            .StartWorkspaceAsync(Command("start-workspace", password: null), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("AGENTUP_ADMIN_PASSWORD"));
    }

    [Test]
    public async Task StartWorkspace_requiresName()
    {
        var result = await Service(new FakeDesktopWindowDriver(), new FakeWorkspaceClient(), new FakeEnvironment())
            .StartWorkspaceAsync(new DebugCommandDto("desktop", "desktop", "start-workspace", null, "test", TimeSpan.FromSeconds(30), false), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("requires a workspace name"));
    }

    private static DesktopCommandService Service(
        FakeDesktopWindowDriver windows,
        FakeWorkspaceClient client,
        FakeEnvironment environment)
        => new(windows, client, new FakeSessionStore(), environment);

    private static DebugCommandDto Command(string action, string? password = "test", TimeSpan? timeout = null)
        => new("desktop", "desktop", action, "Agent-Up", password, timeout ?? TimeSpan.FromSeconds(30), false);
}
