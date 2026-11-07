using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentProcessFactoryTests
{
    [Test]
    public void Create_returnsAnAcpProcessProvider()
    {
        var factory = new AgentProcessFactory(
            new AgentCommandProvider(new ConfigurationBuilder().Build()),
            NullLoggerFactory.Instance);

        Assert.That(factory.Create(), Is.TypeOf<AcpProcessProvider>());
    }

    [Test]
    public void Create_routesTheInProcessAgentAwayFromAnExecutable()
    {
        var commands = new AgentCommandProvider(new ConfigurationBuilder().Build());
        var hosted = new AgentProcessFactory(commands, NullLoggerFactory.Instance, new UnusedModel(), new UnusedTools());
        var bare = new AgentProcessFactory(commands, NullLoggerFactory.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(hosted.Create("ollama", "pm-1"), Is.TypeOf<InProcessAgentProcessProvider>());
            Assert.That(hosted.Create("claude", "author-1"), Is.TypeOf<AcpProcessProvider>());
            Assert.Throws<InvalidOperationException>(() => bare.Create("ollama", "pm-1"));
        });
    }

    [Test]
    public async Task InProcessAgent_isAvailableOnceItsModelIsNamed()
    {
        var unset = new AgentCommandProvider(new ConfigurationBuilder().Build());
        var set = new AgentCommandProvider(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_OLLAMA_MODEL"] = "qwen3-vl" })
            .Build());

        var without = await unset.IsAvailableAsync("ollama", CancellationToken.None);
        var with = await set.IsAvailableAsync("ollama", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(without, Is.False);
            Assert.That(with, Is.True);
        });
    }

    private sealed class UnusedModel : IInProcessChatModel
    {
        public bool Configured => true;

        public Task<InProcessChatMessage> ChatAsync(
            IReadOnlyList<InProcessChatMessage> messages,
            IReadOnlyList<InProcessTool> tools,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class UnusedTools : IInProcessToolHost
    {
        public Task<IReadOnlyList<InProcessTool>> ListAsync(string workspaceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InProcessToolResult> CallAsync(string workspaceId, string tool, System.Text.Json.JsonElement arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
