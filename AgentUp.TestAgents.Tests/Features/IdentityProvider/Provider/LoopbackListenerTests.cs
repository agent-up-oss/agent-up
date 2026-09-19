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
            // Held, not merely known to have been free a moment ago: nothing else can take it.
            Assert.That(() => Occupy(port), Throws.InstanceOf<SocketException>());
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

    // Linux HttpListener.Start for http://+:port/ can throw ArgumentNullException from
    // Monitor.Enter instead of HttpListenerException. That has to be a fallback, not a crash:
    // Android E2E otherwise dies in test-idp before the identity provider answers.
    [Test]
    public void IsRetryableBindFailure_includesTheLinuxWildcardStartCrash()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LoopbackListener.IsRetryableBindFailure(new HttpListenerException()), Is.True);
            Assert.That(LoopbackListener.IsRetryableBindFailure(new SocketException()), Is.True);
            Assert.That(LoopbackListener.IsRetryableBindFailure(new ArgumentNullException()), Is.True);
            Assert.That(LoopbackListener.IsRetryableBindFailure(new ArgumentException()), Is.True);
            Assert.That(LoopbackListener.IsRetryableBindFailure(new InvalidOperationException()), Is.False);
        });
    }

    [Test]
    public void Start_bindsWhenTheWildcardPrefixCannotStart()
    {
        var (listener, port) = LoopbackListener.Start(0, Wildcard, Loopback);
        using var bound = listener;

        Assert.Multiple(() =>
        {
            Assert.That(listener.IsListening, Is.True);
            Assert.That(port, Is.GreaterThan(0));
            Assert.That(listener.Prefixes, Is.Not.Empty);
        });
    }

    // Prefixes.Add rejects this before Start runs. That is the ArgumentException path, and it
    // must fall through rather than fail the named port.
    [Test]
    public void Start_fallsBackWhenThePrefixIsNotAValidHttpListenerPrefix()
    {
        var (listener, port) = LoopbackListener.Start(0, _ => ["not-a-prefix"], Prefixes);
        using var bound = listener;

        Assert.Multiple(() =>
        {
            Assert.That(listener.IsListening, Is.True);
            Assert.That(listener.Prefixes.Single(), Is.EqualTo($"http://127.0.0.1:{port}/agentup-test/"));
        });
    }

    [Test]
    public void TryStart_returnsNullWhenAddRejectsThePrefix()
        => Assert.That(LoopbackListener.TryStart(["not-a-prefix"]), Is.Null);

    [Test]
    public void TryStart_returnsNullWhenStartThrowsSocketException()
        => Assert.That(
            LoopbackListener.TryStart(["http://127.0.0.1:1/"], _ => throw new SocketException()),
            Is.Null);

    [Test]
    public void TryStart_returnsNullWhenStartThrowsTheLinuxWildcardArgumentNullException()
        => Assert.That(
            LoopbackListener.TryStart(["http://+:1/"], _ => throw new ArgumentNullException()),
            Is.Null);

    private static IEnumerable<string> Prefixes(int port) => [$"http://127.0.0.1:{port}/agentup-test/"];

    private static IEnumerable<string> Wildcard(int port) => [$"http://+:{port}/"];

    private static IEnumerable<string> Loopback(int port) =>
        [$"http://127.0.0.1:{port}/", $"http://localhost:{port}/"];

    private static TcpListener Occupied(out int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static void Occupy(int port)
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
