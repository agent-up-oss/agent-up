using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Tests.Features.Agents.Unit;

namespace AgentUp.Desktop.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class AgentsControllerTests
{
    [Test]
    public async Task SendAsync_delegatesAcrossTheSliceBoundary()
    {
        var provider = new FakeAgentApiProvider();
        await new AgentsController(new AgentChatService(provider)).SendAsync("ws", "hello", CancellationToken.None);
        Assert.That(provider.Sent, Is.EqualTo(("ws", "hello")));
    }
}
