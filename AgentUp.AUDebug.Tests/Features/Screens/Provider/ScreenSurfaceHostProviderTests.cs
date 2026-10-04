using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class ScreenSurfaceHostProviderTests
{
    [Test]
    public async Task Start_leavesAnAlreadyRunningDesktopAlone()
    {
        var processes = new FakeProcessRunner();
        var probe = new FakeScreenReadyProbe { DesktopWindowPresent = true };
        var host = Host(processes, probe, nixShell: false);

        await host.StartAsync(ProductSurface.Desktop, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started, Is.Empty);
            Assert.That(probe.DesktopWaits, Is.Zero);
        });
    }

    [Test]
    public async Task Start_leavesAnAlreadyServedMobileAlone()
    {
        var processes = new FakeProcessRunner();
        var probe = new FakeScreenReadyProbe();
        probe.ReadyUrls[DebugLayout.MobileUrl] = true;
        var host = Host(processes, probe, nixShell: false);

        await host.StartAsync(ProductSurface.Mobile, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started, Is.Empty);
            Assert.That(probe.UrlWaits, Is.Zero);
        });
    }

    [Test]
    public async Task Start_runsDesktopThroughNixWhenTheHostHasIt()
    {
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: true);

        await host.StartAsync(ProductSurface.Desktop, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started.Single().FileName, Is.EqualTo("bash"));
            Assert.That(processes.Started.Single().Arguments.Single(), Does.EndWith("run-desktop.sh"));
        });
    }

    [Test]
    public async Task Start_runsDesktopDirectlyWhereThereIsNoNix()
    {
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: false);

        await host.StartAsync(ProductSurface.Desktop, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started.Single().FileName, Is.EqualTo("dotnet"));
            Assert.That(processes.Started.Single().Arguments, Does.Contain("run"));
        });
    }

    [Test]
    public async Task Start_servesMobileDirectlyWhereThereIsNoNix()
    {
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: false);

        await host.StartAsync(ProductSurface.Mobile, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(processes.Started.Single().FileName, Is.EqualTo("node"));
            Assert.That(processes.Started.Single().Arguments.Single(), Is.EqualTo("scripts/serve-web.mjs"));
            Assert.That(processes.Started.Single().Environment!["WEB_PORT"], Is.EqualTo(DebugLayout.MobileWebPort));
        });
    }

    [Test]
    public async Task Start_servesMobileThroughNpmWhenTheHostHasNix()
    {
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: true);

        await host.StartAsync(ProductSurface.Mobile, CancellationToken.None);

        Assert.That(processes.Started.Single().FileName, Is.EqualTo("npm"));
    }

    [Test]
    public async Task Start_givesDesktopTheDebugDisplay()
    {
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: false);

        await host.StartAsync(ProductSurface.Desktop, CancellationToken.None);

        Assert.That(processes.Started.Single().Environment!["DISPLAY"], Is.EqualTo(":0"));
    }

    [Test]
    public async Task Start_drainsTheHostIntoALogSoABlockedPipeCannotLookLikeASlowStart()
    {
        var root = NewRoot();
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: false, root: root);

        await host.StartAsync(ProductSurface.Mobile, CancellationToken.None);
        await host.DisposeAsync();

        Assert.That(File.Exists(Path.Join(root, ".git", "agent-up", "au-debug", "logs", "screens-mobile.log")), Is.True);
    }

    [Test]
    public async Task Dispose_stopsOnlyWhatItStarted()
    {
        var processes = new FakeProcessRunner();
        var host = Host(processes, new FakeScreenReadyProbe(), nixShell: false);
        await host.StartAsync(ProductSurface.Mobile, CancellationToken.None);

        await host.DisposeAsync();

        Assert.That(processes.Killed, Has.Count.EqualTo(1));
    }

    private static ScreenSurfaceHostProvider Host(
        FakeProcessRunner processes,
        FakeScreenReadyProbe probe,
        bool nixShell,
        string? root = null)
    {
        var environment = new FakeEnvironment();
        if (nixShell)
            environment.Executables["nix-shell"] = "/usr/bin/nix-shell";
        return new ScreenSurfaceHostProvider(processes, environment, new FakePathValidator(root ?? NewRoot()), probe);
    }

    private static string NewRoot()
    {
        var root = Path.Join(Path.GetTempPath(), $"au-debug-screens-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
