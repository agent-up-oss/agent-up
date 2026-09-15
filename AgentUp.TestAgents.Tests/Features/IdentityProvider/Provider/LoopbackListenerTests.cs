using System.Net;
using System.Net.Sockets;
using AgentUp.TestAgents.Shared.Providers;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Provider;

// Every listener these agents open goes through here, so the race it exists to close is the thing
// worth pinning: a port that was free a moment ago is not a port you hold.
[TestFixture, CancelAfter(60_000)]
public sealed class LoopbackListenerTests
{
    [Test]
    public void Start_bindsAndHoldsAFreePort()
    {
        var (listener, port) = LoopbackListener.Start(0, Prefixes);
        using var bound = listener;

        Assert.Multiple(() =>
        {
            Assert.That(listener.IsListening, Is.True);
            Assert.That(listener.Prefixes.Single(), Is.EqualTo($"http://127.0.0.1:{port}/agentup-test/"));
            Assert.That(Occupy(port), Throws.InstanceOf<SocketException>(),
                "The port is held by the listener, not merely known to have been free a moment ago");
        });
    }

    // The race itself: the port that was handed out has already been taken by the time it is
    // bound. That has to cost another attempt, not the run.
    [Test]
    public void Start_takesAnotherPortWhenTheOneItWasHandedIsAlreadyTaken()
    {
        using var occupied = Occupied(out var taken);
        var offered = new Queue<int>([taken, taken, FreePort()]);

        var (listener, port) = LoopbackListener.Start(offered.Dequeue, Prefixes);
        using var bound = listener;

        Assert.Multiple(() =>
        {
            Assert.That(listener.IsListening, Is.True);
            Assert.That(port, Is.Not.EqualTo(taken));
            Assert.That(offered, Is.Empty, "Both collisions were retried rather than surfaced");
        });
    }

    [Test]
    public void Start_givesUpRatherThanRetryingForeverWhenNoPortCanBeBound()
    {
        using var occupied = Occupied(out var taken);

        Assert.That(() => LoopbackListener.Start(() => taken, Prefixes), Throws.InstanceOf<HttpListenerException>());
    }

    // A caller that names its port gets that port or nothing: quietly binding a different one
    // would leave whatever was told to connect there talking to no one.
    [Test]
    public void Start_refusesToSubstituteAnotherPortForOneItWasNamed()
    {
        using var occupied = Occupied(out var taken);

        Assert.That(() => LoopbackListener.Start(taken, Prefixes), Throws.InstanceOf<HttpListenerException>());
    }

    // Binding every interface needs a URL reservation on Windows, so falling through to the next
    // prefix set is what keeps these agents working there.
    [Test]
    public void Start_fallsBackToTheNextPrefixSetWhenTheFirstCannotBeBound()
    {
        using var occupied = Occupied(out var taken);

        var (listener, port) = LoopbackListener.Start(0, _ => [$"http://127.0.0.1:{taken}/agentup-test/"], Prefixes);
        using var bound = listener;

        Assert.Multiple(() =>
        {
            Assert.That(listener.IsListening, Is.True);
            Assert.That(listener.Prefixes.Single(), Is.EqualTo($"http://127.0.0.1:{port}/agentup-test/"));
        });
    }

    private static IEnumerable<string> Prefixes(int port) => [$"http://127.0.0.1:{port}/agentup-test/"];

    private static TcpListener Occupied(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static TestDelegate Occupy(int port) => () =>
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
    };

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
