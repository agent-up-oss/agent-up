using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentLoopbackEndpointTests
{
    [Test]
    public void Constructor_keepsTheSocketUnderTheDataDirectory()
    {
        var root = Directory.CreateTempSubdirectory("agent-loopback-");
        try
        {
            var endpoint = new AgentLoopbackEndpoint(root.FullName);

            Assert.Multiple(() =>
            {
                Assert.That(endpoint.SocketPath, Does.StartWith(root.FullName));
                Assert.That(endpoint.SocketPath, Does.EndWith(".sock"));
                Assert.That(Directory.Exists(Path.GetDirectoryName(endpoint.SocketPath)), Is.True);
                Assert.That(System.Text.Encoding.UTF8.GetByteCount(endpoint.SocketPath),
                    Is.LessThanOrEqualTo(AgentLoopbackEndpoint.UnixPathMaxBytes));
            });
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void Constructor_fallsBackToTempWhenTheDataDirectoryMakesThePathTooLong()
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), new string('d', 120)));
        try
        {
            var endpoint = new AgentLoopbackEndpoint(root.FullName);

            Assert.Multiple(() =>
            {
                Assert.That(endpoint.SocketPath, Does.Not.StartWith(root.FullName));
                Assert.That(System.Text.Encoding.UTF8.GetByteCount(endpoint.SocketPath),
                    Is.LessThanOrEqualTo(AgentLoopbackEndpoint.UnixPathMaxBytes));
            });
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void CombineHostingUrls_keepsConfiguredTcpListenersBesideTheUnixSocket()
    {
        var urls = AgentLoopbackEndpoint.CombineHostingUrls("http://127.0.0.1:5001", "/tmp/agent.sock");

        Assert.That(urls, Is.EqualTo(new[] { "http://127.0.0.1:5001", "http://unix:/tmp/agent.sock" }));
    }

    [Test]
    public void TryRun_ignoresOrdinaryServerArgs()
    {
        Assert.That(AgentSandboxLoopbackBridge.TryRun(["--urls=http://127.0.0.1:0"], out var exit), Is.False);
        Assert.That(exit, Is.EqualTo(0));
    }

    [Test]
    public void HostMounts_doesNotMountSystemBinDirectories()
    {
        var mounts = AgentSandboxLoopbackBridge.HostMounts(["/usr/bin/dotnet", "exec", "/opt/server/AgentUp.Server.dll"]);

        Assert.Multiple(() =>
        {
            Assert.That(mounts, Does.Contain("/usr/bin/dotnet"));
            Assert.That(mounts, Does.Not.Contain("/usr/bin"));
            Assert.That(mounts, Does.Contain("/opt/server"));
        });
    }

    [Test]
    public void Wrap_putsTheBridgeInFrontOfTheAgent()
    {
        var wrapped = AgentSandboxLoopbackBridge.Wrap("/tmp/agent.sock", 5001, "/opt/opencode", ["acp"]);

        Assert.Multiple(() =>
        {
            Assert.That(wrapped, Does.Contain(AgentSandboxLoopbackBridge.Flag));
            Assert.That(wrapped, Does.Contain("/tmp/agent.sock"));
            Assert.That(wrapped, Does.Contain("5001"));
            Assert.That(wrapped[^2], Is.EqualTo("/opt/opencode"));
            Assert.That(wrapped[^1], Is.EqualTo("acp"));
        });
    }
}
