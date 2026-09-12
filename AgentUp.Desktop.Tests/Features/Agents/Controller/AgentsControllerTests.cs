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
        var controller = new AgentsController(new AgentChatService(provider));
        await controller.SendAsync("ws", "hello", CancellationToken.None);
        await controller.GetAsync("ws", CancellationToken.None);
        await controller.ScheduleAsync("ws", "Codex", CancellationToken.None);
        await controller.AuthenticateAsync("ws", "chatgpt", CancellationToken.None);
        await controller.DecideAsync("ws", "req", "allow", CancellationToken.None);
        await controller.StopAsync("ws", CancellationToken.None);
        var streamed = 0;
        await foreach (var _ in controller.EventsAsync("ws", 0, CancellationToken.None))
            streamed++;

        Assert.Multiple(() =>
        {
            Assert.That(provider.Sent, Is.EqualTo(("ws", "hello")));
            Assert.That(provider.Authenticated, Is.EqualTo(("ws", "chatgpt")));
            Assert.That(provider.Decided, Is.EqualTo(("ws", "req", "allow")));
            Assert.That(provider.Stopped, Is.EqualTo("ws"));
            Assert.That(streamed, Is.EqualTo(0));
        });
    }
}
