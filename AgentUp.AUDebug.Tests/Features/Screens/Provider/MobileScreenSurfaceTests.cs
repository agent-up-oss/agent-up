using System.Diagnostics;
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

    [Test]
    public async Task Open_connectsToTheDebuggerThenDrivesAndCapturesThePage()
    {
        using var debugger = new FakeChromiumDebugEndpoint
        {
            JsonListFailures = 1,
            MissingPointReplies = 2
        };
        var processes = new FakeProcessRunner();
        var environment = new FakeEnvironment();
        environment.Executables["chromium"] = "/usr/bin/chromium";
        var root = NewRoot();
        using var browser = StartHeldBrowser();
        processes.StartOverride = _ => browser;
        var surface = new MobileScreenSurface(processes, environment, new FakePathValidator(root), debugger.Port);
        var shot = Path.Join(root, "mobile.png");
        var pid = browser.Id;

        try
        {
            await surface.OpenAsync(CancellationToken.None);
            await surface.RunAsync(
                [
                    ScreenStepDto.Navigate("/apps", 0),
                    ScreenStepDto.Tap("Git", 0),
                    ScreenStepDto.Fill("Server URL", "demo") with { DelayMs = 0 },
                    ScreenStepDto.Settle(0)
                ],
                CancellationToken.None);
            await surface.CaptureAsync(shot, CancellationToken.None);
            await surface.DisposeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(debugger.Methods, Does.Contain("Emulation.setDeviceMetricsOverride"));
                Assert.That(debugger.Methods, Does.Contain("Page.navigate"));
                Assert.That(debugger.Methods, Does.Contain("Runtime.evaluate"));
                Assert.That(debugger.Methods, Does.Contain("Input.dispatchMouseEvent"));
                Assert.That(debugger.Methods, Does.Contain("Input.insertText"));
                Assert.That(debugger.Methods, Does.Contain("Page.captureScreenshot"));
                Assert.That(File.ReadAllBytes(shot), Is.EqualTo(FakeChromiumDebugEndpoint.Png));
                Assert.That(processes.Killed, Does.Contain(pid));
            });
        }
        finally
        {
            TryKill(pid);
        }
    }

    [Test]
    public void Capture_reportsWhenTheSurfaceWasNotOpened()
    {
        var surface = Surface(new FakeProcessRunner(), new FakeEnvironment());

        Assert.That(
            async () => await surface.CaptureAsync("/tmp/mobile.png", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("were not opened"));
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

    private static Process StartHeldBrowser()
        => Process.Start(new ProcessStartInfo
        {
            FileName = "sleep",
            Arguments = "60",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        })!;

    private static void TryKill(int pid)
    {
        try
        {
            Process.GetProcessById(pid).Kill(entireProcessTree: true);
        }
        catch (ArgumentException exception)
        {
            TestContext.WriteLine(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            TestContext.WriteLine(exception.Message);
        }
    }
}
