using System.Net;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class ScreenReadyProbeProviderTests
{
    [Test]
    public async Task IsReady_isTrueOnceThePageIsServed()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{FreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var served = Respond(listener);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            Assert.That(await Probe(http, new FakeProcessRunner()).IsReadyAsync(prefix, CancellationToken.None), Is.True);
        }
        finally
        {
            await served;
            listener.Stop();
        }
    }

    [Test]
    public async Task IsReady_isFalseWhileNothingIsListening()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(250) };

        var ready = await Probe(http, new FakeProcessRunner()).IsReadyAsync("http://127.0.0.1:1/", CancellationToken.None);

        Assert.That(ready, Is.False);
    }

    [Test]
    public async Task WaitForUrl_returnsOnceTheHostAnswers()
    {
        using var listener = new HttpListener();
        var prefix = $"http://127.0.0.1:{FreePort()}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var served = Respond(listener);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            await Probe(http, new FakeProcessRunner()).WaitForUrlAsync(prefix, CancellationToken.None);
            Assert.Pass();
        }
        finally
        {
            await served;
            listener.Stop();
        }
    }

    [Test]
    public async Task HasDesktopWindow_asksXdotoolForTheDesktopWindowClass()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        using var http = new HttpClient();

        var present = await Probe(http, processes).HasDesktopWindowAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(present, Is.True);
            Assert.That(processes.Ran.Single().Arguments, Does.Contain(DebugLayout.DesktopWindowClass));
        });
    }

    [Test]
    public async Task HasDesktopWindow_isFalseWhileTheSearchFindsNothing()
    {
        var processes = new FakeProcessRunner { NextResult = new(1, "", "") };
        using var http = new HttpClient();

        Assert.That(await Probe(http, processes).HasDesktopWindowAsync(CancellationToken.None), Is.False);
    }

    [Test]
    public async Task WaitForDesktopWindow_returnsOnceTheWindowIsThere()
    {
        var processes = new FakeProcessRunner { NextResult = new(0, "4242\n", "") };
        using var http = new HttpClient();

        await Probe(http, processes).WaitForDesktopWindowAsync(CancellationToken.None);

        Assert.Pass();
    }

    private static ScreenReadyProbeProvider Probe(HttpClient http, FakeProcessRunner processes)
    {
        var environment = new FakeEnvironment();
        environment.Executables["xdotool"] = "/bin/xdotool";
        return new ScreenReadyProbeProvider(http, processes, environment, new FakePathValidator("/tmp/au-debug-screens-probe"));
    }

    private static Task Respond(HttpListener listener)
        => Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = 200;
            context.Response.Close();
        });

    private static int FreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
