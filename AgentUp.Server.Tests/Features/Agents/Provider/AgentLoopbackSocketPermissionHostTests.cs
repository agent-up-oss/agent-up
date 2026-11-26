using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentLoopbackSocketPermissionHostTests
{
    [Test]
    public async Task Restrict_makesTheSocketOwnerOnlyOnceKestrelHasCreatedIt()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var root = Directory.CreateTempSubdirectory("agent-loopback-perm-");
        var lifetime = new Lifetime();
        try
        {
            var endpoint = new AgentLoopbackEndpoint(root.FullName);
            File.WriteAllText(endpoint.SocketPath, string.Empty);
            var host = new AgentLoopbackSocketPermissionHost(
                endpoint,
                lifetime,
                NullLogger<AgentLoopbackSocketPermissionHost>.Instance);

            await host.StartAsync(CancellationToken.None);
            lifetime.Start();

            Assert.That(
                File.GetUnixFileMode(endpoint.SocketPath),
                Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public async Task StopAsync_removesTheSocketAndSwallowsADeleteFailure()
    {
        var root = Directory.CreateTempSubdirectory("agent-loopback-stop-");
        try
        {
            var endpoint = new AgentLoopbackEndpoint(root.FullName);
            File.WriteAllText(endpoint.SocketPath, string.Empty);
            var host = new AgentLoopbackSocketPermissionHost(
                endpoint,
                new Lifetime(),
                NullLogger<AgentLoopbackSocketPermissionHost>.Instance);

            await host.StopAsync(CancellationToken.None);
            Assert.That(File.Exists(endpoint.SocketPath), Is.False);

            Directory.CreateDirectory(endpoint.SocketPath);
            File.WriteAllText(Path.Join(endpoint.SocketPath, "keep"), string.Empty);
            await host.StopAsync(CancellationToken.None);
            Assert.That(Directory.Exists(endpoint.SocketPath), Is.True);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public async Task Restrict_swallowsAChmodFailure()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var root = Directory.CreateTempSubdirectory("agent-loopback-chmod-");
        var lifetime = new Lifetime();
        try
        {
            var endpoint = new AgentLoopbackEndpoint(root.FullName);
            File.CreateSymbolicLink(endpoint.SocketPath, "/proc/self/mem");
            var host = new AgentLoopbackSocketPermissionHost(
                endpoint,
                lifetime,
                NullLogger<AgentLoopbackSocketPermissionHost>.Instance);

            await host.StartAsync(CancellationToken.None);
            lifetime.Start();
            await host.StopAsync(CancellationToken.None);
        }
        finally
        {
            root.Delete(true);
        }
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;

        public void Start() => _started.Cancel();

        public void StopApplication()
        {
            _stopping.Cancel();
            _stopped.Cancel();
        }
    }
}
