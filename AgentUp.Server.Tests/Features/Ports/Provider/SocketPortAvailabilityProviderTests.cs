using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.Ports.Providers;

namespace AgentUp.Server.Tests.Features.Ports.Provider;

[TestFixture]
public sealed class SocketPortAvailabilityProviderTests
{
    [Test]
    public void ArePortsAvailable_returns_true_for_an_empty_range()
        => Assert.That(new SocketPortAvailabilityProvider().ArePortsAvailable(1, 0), Is.True);

    [Test]
    public void ArePortsAvailable_returns_false_when_a_port_is_bound()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.That(new SocketPortAvailabilityProvider().ArePortsAvailable(port, 1), Is.False);
    }

    [Test]
    public void ArePortsAvailable_returns_true_after_the_bound_port_is_released()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        Assert.That(new SocketPortAvailabilityProvider().ArePortsAvailable(port, 1), Is.True);
    }
}
