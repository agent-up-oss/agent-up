using System.Net;
using System.Net.Sockets;
using AgentUp.AUDebug.Features.Validation.Providers;

namespace AgentUp.AUDebug.Tests.Features.Validation.Provider;

[TestFixture]
public sealed class LoopbackEndpointOccupancyProviderTests
{
    [Test]
    public async Task IsOccupiedAsync_isTrueWhenAListenerIsBound()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var occupied = await new LoopbackEndpointOccupancyProvider()
            .IsOccupiedAsync($"http://127.0.0.1:{port}", CancellationToken.None);

        Assert.That(occupied, Is.True);
    }

    [Test]
    public async Task IsOccupiedAsync_isFalseWhenNothingIsListening()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var occupied = await new LoopbackEndpointOccupancyProvider()
            .IsOccupiedAsync($"http://127.0.0.1:{port}", CancellationToken.None);

        Assert.That(occupied, Is.False);
    }

    [Test]
    public void IsOccupiedAsync_namesAnInvalidUrl()
    {
        Assert.That(
            async () => await new LoopbackEndpointOccupancyProvider()
                .IsOccupiedAsync("not-a-url", CancellationToken.None),
            Throws.InvalidOperationException.With.Message.Contains("not a URL"));
    }
}
