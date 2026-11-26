using System.Net;
using System.Net.Sockets;
using System.Text;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSandboxLoopbackBridgeTests
{
    [Test]
    public void TryRun_requiresTheSocketAndTheAgentCommand()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => AgentSandboxLoopbackBridge.TryRun(
                    [AgentSandboxLoopbackBridge.Flag, "--port", "1", "--", "true"],
                    out _),
                Throws.InvalidOperationException.With.Message.Contains("--socket"));
            Assert.That(
                () => AgentSandboxLoopbackBridge.TryRun(
                    [AgentSandboxLoopbackBridge.Flag, "--socket", "/tmp/x.sock", "--port", "1", "--"],
                    out _),
                Throws.InvalidOperationException.With.Message.Contains("after --"));
        });
    }

    [Test]
    public void TryRun_copiesLoopbackTcpOntoTheUnixSocket()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/python3"))
            return;

        var root = Directory.CreateTempSubdirectory("agent-bridge-");
        try
        {
            var socketPath = Path.Join(root.FullName, "s.sock");
            using var unix = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            unix.Bind(new UnixDomainSocketEndPoint(socketPath));
            unix.Listen(1);
            var payload = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var accept = Task.Run(() =>
            {
                using var client = unix.Accept();
                using var stream = new NetworkStream(client, ownsSocket: false);
                var buffer = new byte[16];
                var read = stream.Read(buffer);
                payload.TrySetResult(Encoding.ASCII.GetString(buffer, 0, read));
                stream.Write("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nOK"u8);
            });

            var port = FreePort();
            var python =
                $"import socket,sys;s=socket.create_connection(('127.0.0.1',{port}));s.sendall(b'PING');s.shutdown(socket.SHUT_WR);sys.stdout.write(s.recv(64).decode())";
            var ran = AgentSandboxLoopbackBridge.TryRun(
                [
                    AgentSandboxLoopbackBridge.Flag,
                    "--socket",
                    socketPath,
                    "--port",
                    $"{port}",
                    "--",
                    "/usr/bin/python3",
                    "-c",
                    python
                ],
                out var exit);
            accept.Wait(TimeSpan.FromSeconds(5));

            Assert.Multiple(() =>
            {
                Assert.That(ran, Is.True);
                Assert.That(exit, Is.EqualTo(0));
                Assert.That(payload.Task.Result, Is.EqualTo("PING"));
            });
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void TryRun_survivesWhenTheUnixSocketCannotBeReached()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/python3"))
            return;

        var port = FreePort();
        var python =
            $"import socket;s=socket.create_connection(('127.0.0.1',{port}));s.sendall(b'PING');s.shutdown(socket.SHUT_WR);s.recv(8)";
        var ran = AgentSandboxLoopbackBridge.TryRun(
            [
                AgentSandboxLoopbackBridge.Flag,
                "--socket",
                "/tmp/agent-up-missing-loopback.sock",
                "--port",
                $"{port}",
                "--",
                "/usr/bin/python3",
                "-c",
                python
            ],
            out var exit);

        Assert.Multiple(() =>
        {
            Assert.That(ran, Is.True);
            Assert.That(exit, Is.EqualTo(0));
        });
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
