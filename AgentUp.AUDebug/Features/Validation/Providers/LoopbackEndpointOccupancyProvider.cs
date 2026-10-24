using System.Net.Sockets;
using AgentUp.AUDebug.Features.Validation.Interfaces;

namespace AgentUp.AUDebug.Features.Validation.Providers;

public sealed class LoopbackEndpointOccupancyProvider : ILoopbackEndpointOccupancy
{
    public async Task<bool> IsOccupiedAsync(string url, CancellationToken cancellationToken)
    {
        Uri uri;
        try
        {
            uri = new Uri(url);
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException)
        {
            throw new InvalidOperationException($"Isolated validation needs a loopback Server URL. '{url}' is not a URL.", exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(uri.Host, uri.Port, timeout.Token);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
