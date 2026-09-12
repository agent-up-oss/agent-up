using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Interfaces;
using AgentUp.Desktop.Features.Agents.Services;
using System.Runtime.CompilerServices;

namespace AgentUp.Desktop.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentChatServiceTests
{
    [Test]
    public async Task SendAsync_preservesWorkspaceAndMessage()
    {
        var provider = new FakeAgentApiProvider();
        var service = new AgentChatService(provider);
        await service.SendAsync("ws-1", "fix it", CancellationToken.None);
        await service.GetAsync("ws-1", CancellationToken.None);
        await service.ScheduleAsync("ws-1", "Codex", CancellationToken.None);
        await service.AuthenticateAsync("ws-1", "chatgpt", CancellationToken.None);
        await service.DecideAsync("ws-1", "req", "allow", CancellationToken.None);
        await service.StopAsync("ws-1", CancellationToken.None);
        var streamed = 0;
        await foreach (var _ in service.EventsAsync("ws-1", 0, CancellationToken.None))
            streamed++;

        Assert.Multiple(() =>
        {
            Assert.That(provider.Sent, Is.EqualTo(("ws-1", "fix it")));
            Assert.That(provider.Authenticated, Is.EqualTo(("ws-1", "chatgpt")));
            Assert.That(provider.Decided, Is.EqualTo(("ws-1", "req", "allow")));
            Assert.That(provider.Stopped, Is.EqualTo("ws-1"));
            Assert.That(streamed, Is.EqualTo(0));
        });
    }
}

internal sealed class FakeAgentApiProvider : IAgentApiProvider
{
    public (string, string)? Sent { get; private set; }
    public (string, string)? Authenticated { get; private set; }
    public (string, string, string)? Decided { get; private set; }
    public string? Stopped { get; private set; }
    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken) => Task.FromResult<AgentSessionDto?>(null);
    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken) => Task.FromResult<AgentSessionDto?>(null);
    public Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken) { Sent = (workspaceId, message); return Task.CompletedTask; }
    public Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken) { Authenticated = (workspaceId, methodId); return Task.CompletedTask; }
    public Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) { Decided = (workspaceId, requestId, optionId); return Task.CompletedTask; }
    public Task StopAsync(string workspaceId, CancellationToken cancellationToken) { Stopped = workspaceId; return Task.CompletedTask; }
    public async IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken) { await Task.CompletedTask; yield break; }
}
