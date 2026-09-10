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
        await new AgentChatService(provider).SendAsync("ws-1", "fix it", CancellationToken.None);
        Assert.That(provider.Sent, Is.EqualTo(("ws-1", "fix it")));
    }
}

internal sealed class FakeAgentApiProvider : IAgentApiProvider
{
    public (string, string)? Sent { get; private set; }
    public Task<AgentSessionDto?> GetAsync(string workspaceId, CancellationToken cancellationToken) => Task.FromResult<AgentSessionDto?>(null);
    public Task<AgentSessionDto?> ScheduleAsync(string workspaceId, string agent, CancellationToken cancellationToken) => Task.FromResult<AgentSessionDto?>(null);
    public Task SendAsync(string workspaceId, string message, CancellationToken cancellationToken) { Sent = (workspaceId, message); return Task.CompletedTask; }
    public Task AuthenticateAsync(string workspaceId, string methodId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task DecideAsync(string workspaceId, string requestId, string optionId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(string workspaceId, CancellationToken cancellationToken) => Task.CompletedTask;
    public async IAsyncEnumerable<AgentEventDto> EventsAsync(string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken) { await Task.CompletedTask; yield break; }
}
