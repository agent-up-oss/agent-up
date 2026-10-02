using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class MobileScreenSurfaceTests
{
    [Test]
    public void Open_launchesChromiumOnTheConnectScreenAtPhoneMetrics()
    {
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/usr/bin/chromium";
        var surface = Surface(processes, environment);
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(60));

        Assert.That(
            async () => await surface.OpenAsync(cancelled.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(processes.Started.Single().FileName, Is.EqualTo("chromium"));
            Assert.That(processes.Started.Single().Arguments, Does.Contain($"{DebugLayout.MobileUrl}/connect"));
            Assert.That(
                processes.Started.Single().Arguments,
                Does.Contain($"--window-size={DebugLayout.MobileScreenWidth},{DebugLayout.MobileScreenHeight}"));
        });
    }

    [Test]
    public void Open_fallsBackToNixChromiumWhereThereIsNone()
    {
        var processes = new FakeProcessRunner();
        var surface = Surface(processes, new FakeEnvironment());
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(60));

        Assert.That(
            async () => await surface.OpenAsync(cancelled.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(processes.Started.Single().FileName, Is.EqualTo("nix-shell"));
            Assert.That(processes.Started.Single().Arguments, Does.Contain("chromium"));
        });
    }

    [Test]
    public void Run_rejectsAStepThatBelongsToTheOtherSurface()
    {
        var surface = Surface(new FakeProcessRunner(), new FakeEnvironment());

        Assert.That(
            async () => await surface.RunAsync([ScreenStepDto.Click(10, 20)], CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("Click"));
    }

    [Test]
    public void Run_reportsAStepDrivenBeforeTheSurfaceWasOpened()
    {
        var surface = Surface(new FakeProcessRunner(), new FakeEnvironment());

        Assert.That(
            async () => await surface.RunAsync([ScreenStepDto.Tap("Git")], CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("were not opened"));
    }

    [Test]
    public async Task Run_settlesWithoutTalkingToThePage()
    {
        var processes = new FakeProcessRunner();
        var surface = Surface(processes, new FakeEnvironment());

        await surface.RunAsync([ScreenStepDto.Settle(1)], CancellationToken.None);

        Assert.That(processes.Started, Is.Empty);
    }

    [Test]
    public async Task Dispose_isSafeBeforeAnythingWasStarted()
    {
        var surface = Surface(new FakeProcessRunner(), new FakeEnvironment());

        await surface.DisposeAsync();

        Assert.That(surface.Surface, Is.EqualTo(ProductSurface.Mobile));
    }

    private static MobileScreenSurface Surface(FakeProcessRunner processes, FakeEnvironment environment)
        => new(processes, environment, new FakePathValidator(NewRoot()), FreePort());

    private static string NewRoot()
    {
        var root = Path.Join(Path.GetTempPath(), $"au-debug-screens-mobile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static int FreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        socket.Start();
        return ((System.Net.IPEndPoint)socket.LocalEndpoint).Port;
    }
}
