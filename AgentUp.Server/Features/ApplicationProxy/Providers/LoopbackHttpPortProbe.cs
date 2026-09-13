using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class LoopbackHttpPortProbe : ILoopbackHttpPortProbe
{
    public bool IsListening(int port)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(new IPEndPoint(IPAddress.Loopback, port));
            return client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
