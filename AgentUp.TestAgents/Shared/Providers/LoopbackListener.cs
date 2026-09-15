using System.Net;
using System.Net.Sockets;

namespace AgentUp.TestAgents.Shared.Providers;

/// <summary>
/// Claims a port by binding the listener that will answer on it.
/// <para>
/// Asking the OS for an ephemeral port and then closing the probe returns a port that is free only
/// until something else takes it. These agents run several listeners at once, next to a Server and
/// a suite doing the same, so that window is wide enough to lose - and losing it reads as a flaky
/// test rather than as the race it is. Nothing here holds a port except the listener that goes on
/// to serve it, and a collision costs another attempt instead of the run.
/// </para>
/// </summary>
public static class LoopbackListener
{
    private const int Attempts = 25;

    /// <summary>
    /// Starts a listener on <paramref name="port"/>, or on a free port of its own choosing when
    /// that is zero. A named port is not negotiable: substituting another would leave whatever was
    /// told to connect there talking to nothing.
    /// <para>
    /// Each entry of <paramref name="preferences"/> is a set of prefixes to try at a port, in
    /// order, so a caller that wants to bind every interface can offer loopback as the next best
    /// thing for a host that will not reserve it.
    /// </para>
    /// </summary>
    public static (HttpListener Listener, int Port) Start(int port, params Func<int, IEnumerable<string>>[] preferences) =>
        port == 0
            ? Start(FreePort, preferences)
            : (Bind(port, preferences) ?? throw Refused($"Nothing could be bound on port {port}."), port);

    internal static (HttpListener Listener, int Port) Start(Func<int> ports, params Func<int, IEnumerable<string>>[] preferences)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var candidate = ports();
            if (Bind(candidate, preferences) is { } listener)
                return (listener, candidate);
        }

        throw Refused($"Could not bind a free loopback port in {Attempts} attempts.");
    }

    private static HttpListener? Bind(int port, Func<int, IEnumerable<string>>[] preferences)
    {
        foreach (var preference in preferences)
        {
            var listener = new HttpListener();
            foreach (var prefix in preference(port))
                listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                return listener;
            }
            catch (Exception exception) when (exception is HttpListenerException or SocketException)
            {
                listener.Close();
            }
        }

        return null;
    }

    private static HttpListenerException Refused(string message) =>
        new((int)SocketError.AddressAlreadyInUse, message);

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
