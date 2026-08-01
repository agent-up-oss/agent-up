using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserMcpServiceTests
{
    [Test]
    public async Task ClickAsync_ReturnsBrowserStateData()
    {
        var store = new BrowserSessionStore();
        var service = new BrowserMcpService(store);
        var state = "{\"url\":\"http://localhost:3000\",\"interactive\":[]}";
        var resultTask = service.ClickAsync("workspace", "#save", CancellationToken.None);

        var command = await store.TryDequeueAsync(
            ["workspace"],
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.That(command, Is.Not.Null);

        store.CompleteCommand(new BrowserCommandResultDto(command!.CommandId, true, state, null));

        var result = await resultTask;

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.EqualTo(state));
    }
}
