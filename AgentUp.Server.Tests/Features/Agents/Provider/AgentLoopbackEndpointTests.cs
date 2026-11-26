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
            });
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Test]
    public void TryRun_ignoresOrdinaryServerArgs()
    {
        Assert.That(AgentSandboxLoopbackBridge.TryRun(["--urls=http://127.0.0.1:0"], out var exit), Is.False);
        Assert.That(exit, Is.EqualTo(0));
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
