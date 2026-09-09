using System.Net;
using System.Net.Sockets;

namespace AgentUp.Tests.Support;

// Loopback port allocation for the local servers the end-to-end tests bind. Asking the OS for
// port 0 keeps the tests free of hard-coded ports on every platform.
internal static class LoopbackPorts
{
    internal static int FindFree()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
