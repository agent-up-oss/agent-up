using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Services;
using AgentUp.AUDebug.Tests.Fake;

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
            new FakeSessionStore(),
            environment).LoginAsync(Command(password: null), CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
    }

    [Test]
    public async Task Login_drivesSurfaceThenScreenshots()
    {
        var surface = new FakeMobileSurfaceDriver();
        var shots = new FakeWebScreenshotDriver();
        var result = await new MobileCommandService(shots, surface, new FakeSessionStore(), new FakeEnvironment())
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
        var result = await new MobileCommandService(shots, surface, new FakeSessionStore(), new FakeEnvironment())
            .ScreenshotAsync(new DebugCommandDto("mobile", "mobile", "screenshot", null, null, TimeSpan.FromSeconds(30), false), CancellationToken.None);

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
            new FakeSessionStore(),
            new FakeEnvironment()).LoginAsync(
            new DebugCommandDto("mobile", "mobile", "login", null, "test", TimeSpan.FromMilliseconds(30), false),
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
            new FakeSessionStore(),
            new FakeEnvironment()).ScreenshotAsync(
            new DebugCommandDto("mobile", "mobile", "screenshot", null, null, TimeSpan.FromSeconds(30), false),
            CancellationToken.None);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.Message, Is.EqualTo("cdp failed"));
    }

    private static DebugCommandDto Command(string? password = "test")
        => new("mobile", "mobile", "login", null, password, TimeSpan.FromSeconds(30), false);
}
