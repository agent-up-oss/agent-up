using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Runs inside the agent's network namespace and copies TCP on the sandbox loopback to the
/// Server's Unix socket, so MCP and the model proxy still look like <c>127.0.0.1</c> to the
/// agent after <c>--unshare-net</c>.
/// </summary>
[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "A new network namespace has loopback down and .NET has no managed API to set IFF_UP; ioctl stays private in this provider.")]
public static class AgentSandboxLoopbackBridge
{
    public const string Flag = "--agent-sandbox-bridge";

    public static bool TryRun(IReadOnlyList<string> args, out int exitCode)
    {
        if (args.Count == 0 || args[0] != Flag)
        {
            exitCode = 0;
            return false;
        }

        exitCode = Run(args);
        return true;
    }

    /// <summary>
    /// The argv the sandbox execs after <c>bwrap --</c>: this Server process in bridge mode,
    /// then the real agent.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string socket, int port, string executable, IReadOnlyList<string> arguments)
        => [.. HostCommand(), Flag, "--socket", socket, "--port", $"{port}", "--", executable, .. arguments];

    public static IReadOnlyList<string> HostCommand()
    {
        var process = Environment.ProcessPath
            ?? throw new InvalidOperationException("The Server process path is unknown.");
        var name = Path.GetFileNameWithoutExtension(process);
        if (name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return [process, "exec", typeof(AgentSandboxLoopbackBridge).Assembly.Location];
        return [process];
    }

    public static IReadOnlyList<string> HostMounts() => HostMounts(HostCommand());

    internal static IReadOnlyList<string> HostMounts(IReadOnlyList<string> command)
    {
        var rooted = command
            .Where(Path.IsPathRooted)
            .SelectMany(part =>
            {
                var directory = Path.GetDirectoryName(part);
                return directory is null || IsSystemBin(directory)
                    ? [part]
                    : new[] { part, directory };
            })
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate!);
        return rooted
            .Append(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Select(Path.GetFullPath)
            .Where(candidate => !IsSystemBin(candidate))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSystemBin(string path) =>
        path is "/usr/bin" or "/bin" or "/usr/sbin" or "/sbin";

    private static int Run(IReadOnlyList<string> args)
    {
        var socketPath = Required(args, "--socket");
        var port = int.Parse(Required(args, "--port"), System.Globalization.CultureInfo.InvariantCulture);
        var command = AfterDash(args);
        if (command.Count == 0)
            throw new InvalidOperationException("The sandbox bridge needs the agent command after --.");

        BringUpLoopback();
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var accept = new Thread(() => Accept(listener, socketPath)) { IsBackground = true };
        accept.Start();
        var start = new ProcessStartInfo(command[0]) { UseShellExecute = false };
        foreach (var argument in command.Skip(1))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {command[0]}.");
        process.WaitForExit();
        listener.Stop();
        return process.ExitCode;
    }

    private static void Accept(TcpListener listener, string socketPath)
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                return;
            }

            var copy = new Thread(() => Copy(client, socketPath)) { IsBackground = true };
            copy.Start();
        }
    }

    private static void Copy(TcpClient client, string socketPath)
    {
        using (client)
        using (var upstream = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            try
            {
                upstream.Connect(new UnixDomainSocketEndPoint(socketPath));
                using var incoming = client.GetStream();
                using var outgoing = new NetworkStream(upstream, ownsSocket: false);
                var toUnix = new Thread(() => CopyOneWay(incoming, outgoing, () => ShutdownSend(upstream))) { IsBackground = true };
                var toTcp = new Thread(() => CopyOneWay(outgoing, incoming, () => ShutdownSend(client.Client))) { IsBackground = true };
                toUnix.Start();
                toTcp.Start();
                toUnix.Join();
                toTcp.Join();
            }
            catch (Exception exception) when (exception is SocketException or IOException or ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static void CopyOneWay(Stream from, Stream to, Action complete)
    {
        try
        {
            from.CopyTo(to);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException)
        {
            return;
        }
        finally
        {
            try
            {
                complete();
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                Trace.WriteLine(exception);
            }
        }
    }

    private static void ShutdownSend(Socket socket)
    {
        if (socket.Connected)
            socket.Shutdown(SocketShutdown.Send);
    }

    /// <summary>
    /// A new network namespace has loopback down. The bridge needs 127.0.0.1, and the only
    /// remaining capability in that namespace is NET_ADMIN so this ioctl can succeed.
    /// </summary>
    private static void BringUpLoopback()
    {
        if (!OperatingSystem.IsLinux())
            return;
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var buffer = new byte[40];
        Encoding.ASCII.GetBytes("lo").CopyTo(buffer, 0);
        if (Ioctl(socket.SafeHandle, Siocgifflags, buffer) != 0)
            return;
        var flags = (short)(BitConverter.ToInt16(buffer, 16) | IffUp | IffRunning);
        BitConverter.GetBytes(flags).CopyTo(buffer, 16);
        _ = Ioctl(socket.SafeHandle, Siocsifflags, buffer);
    }

    private const short IffUp = 1;
    private const short IffRunning = 64;
    private const nuint Siocgifflags = 0x8913;
    private const nuint Siocsifflags = 0x8914;

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(SafeHandle fileDescriptor, nuint request, byte[] argument);

    private static string Required(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (args[index] == name)
                return args[index + 1];
        }

        throw new InvalidOperationException($"The sandbox bridge is missing {name}.");
    }

    private static IReadOnlyList<string> AfterDash(IReadOnlyList<string> args)
    {
        var index = 0;
        while (index < args.Count && args[index] != "--")
            index++;
        return index < args.Count ? args.Skip(index + 1).ToArray() : [];
    }
}
