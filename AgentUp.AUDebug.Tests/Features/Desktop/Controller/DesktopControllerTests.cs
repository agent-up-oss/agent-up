using AgentUp.AUDebug.Features.Desktop.Controllers;
using AgentUp.AUDebug.Features.Desktop.Services;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Desktop.Controller;

[TestFixture]
public sealed class DesktopControllerTests
{
    [Test]
    public async Task Screenshot_writesPath()
    {
        var windows = new FakeDesktopWindowDriver();
        var result = await Controller(windows).RunAsync(Command("screenshot"), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.ArtifactPath, Is.Not.Null);
        Assert.That(windows.Captures, Is.EqualTo(1));
    }

    [Test]
    public async Task UnknownAction_fails()
    {
        var result = await Controller(new FakeDesktopWindowDriver())
            .RunAsync(Command("nope"), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Does.Contain("unknown desktop action"));
    }

    [Test]
    public async Task Login_routesToWindowDriver()
    {
        var windows = new FakeDesktopWindowDriver();
        var result = await Controller(windows).RunAsync(Command("login"), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(windows.LastPassword, Is.EqualTo("test"));
        Assert.That(windows.Captures, Is.EqualTo(1));
    }

    [Test]
    public async Task StartWorkspace_routesToWorkspaceClient()
    {
        var result = await Controller(new FakeDesktopWindowDriver()).RunAsync(Command("start-workspace"), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.Message, Does.Contain("Started workspace"));
    }

    private static DesktopController Controller(FakeDesktopWindowDriver windows)
        => new(new DesktopCommandService(windows, new FakeWorkspaceClient(), new FakeSessionStore(), new FakeEnvironment()));

    private static DebugCommandDto Command(string action)
        => new("desktop", "desktop", action, "Agent-Up", "test", TimeSpan.FromSeconds(30), false);
}
