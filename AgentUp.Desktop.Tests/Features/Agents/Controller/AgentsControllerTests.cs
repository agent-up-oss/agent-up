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
        await controller.BindAsync("ws", "Codex", CancellationToken.None);
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

    [Test]
    public async Task Missing_session_and_schedule_results_remain_null_at_the_controller_boundary()
    {
        var controller = new AgentsController(new AgentChatService(new FakeAgentApiProvider()));

        Assert.That(await controller.GetAsync("missing", CancellationToken.None), Is.Null);
        Assert.That(await controller.ScheduleAsync("missing", "Codex", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task AutonomyCalls_delegateAcrossTheSliceBoundary()
    {
        var provider = new FakeAgentApiProvider();
        var controller = new AgentsController(new AgentChatService(provider));

        await controller.GetAutonomyAsync(CancellationToken.None);
        await controller.PutAutonomyAsync(true, CancellationToken.None);
        await controller.PutLoopActivationAsync("ws-1", true, CancellationToken.None);
        await controller.GetLoopAsync("ws-1", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(provider.AutonomyEnabled, Is.True);
            Assert.That(provider.LoopActivation, Is.EqualTo(("ws-1", true)));
            Assert.That(provider.LoopReads, Is.EqualTo("ws-1"));
        });
    }
}
