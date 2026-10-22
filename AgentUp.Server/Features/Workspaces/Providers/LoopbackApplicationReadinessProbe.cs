using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.Workspaces.Interfaces;

namespace AgentUp.Server.Features.Workspaces.Providers;

public sealed class LoopbackApplicationReadinessProbe : IApplicationReadinessProbe, IDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _http = new() { Timeout = ProbeTimeout };

    public async Task<bool> TcpReadyAsync(int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            return client.Connected;
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return false;
        }
    }

    public async Task<bool> HttpReadyAsync(int port, string path, CancellationToken cancellationToken)
    {
        var url = $"http://127.0.0.1:{port}{path}";
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return (int)response.StatusCode < 400;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}
