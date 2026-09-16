using System.Net;
using System.Net.Sockets;

namespace AgentUp.Server.Tests.Support;

// A port the OS calls free is only free until something takes it, and a CI runner has a machine
// full of suites binding listeners at the same moment. Binding is what claims a port, so the
// listener that will answer on it is the only thing that ever holds it here, and losing the race
// costs another attempt rather than reading as a failing test.
internal static class LoopbackListener
{
    private const int Attempts = 25;

    internal static (HttpListener Listener, int Port) Start(Func<int, string> prefix)
    {
        for (var attempt = 1; ; attempt++)
        {
            var port = FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix(port));

            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (Exception exception) when (exception is HttpListenerException or SocketException && attempt < Attempts)
            {
                listener.Close();
            }
        }
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
