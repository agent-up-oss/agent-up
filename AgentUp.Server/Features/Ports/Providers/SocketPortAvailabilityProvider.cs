using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.Ports.Interfaces;

namespace AgentUp.Server.Features.Ports.Providers;

public sealed class SocketPortAvailabilityProvider : IPortAvailabilityProvider
{
    public bool ArePortsAvailable(int basePort, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!IsPortAvailable(basePort + i))
                return false;
        }

        return true;
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
