using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserSessionStoreTests
{
    [Test]
    public async Task DispatchAsync_ReturnsCompletedResult_WhenCommandIsCompleted()
    {
        var store = new BrowserSessionStore();
        var command = Command(Guid.NewGuid(), "workspace");
        var dispatch = store.DispatchAsync(command, TimeSpan.FromSeconds(1), CancellationToken.None);

        var pending = await store.TryDequeueAsync(["workspace"], TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.That(pending, Is.EqualTo(command));

        store.CompleteCommand(new BrowserCommandResultDto(command.CommandId, true, "{}", null));
        var result = await dispatch;

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.EqualTo("{}"));
        });
    }

    [Test]
    public async Task DispatchAsync_ReturnsTimeout_WhenDesktopDoesNotCompleteCommand()
    {
        var store = new BrowserSessionStore();
        var command = Command(Guid.NewGuid(), "workspace");

        var result = await store.DispatchAsync(command, TimeSpan.FromMilliseconds(20), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("timed out"));
        });
    }

    [Test]
    public async Task DispatchAsync_ReturnsCancelled_WhenCallerCancels()
    {
        var store = new BrowserSessionStore();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await store.DispatchAsync(Command(Guid.NewGuid(), "workspace"), TimeSpan.FromSeconds(1), cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("Request was cancelled."));
        });
    }

    [Test]
    public async Task TryDequeueAsync_SkipsTimedOutCommands()
    {
        var store = new BrowserSessionStore();
        var timedOut = Command(Guid.NewGuid(), "workspace");
        await store.DispatchAsync(timedOut, TimeSpan.FromMilliseconds(20), CancellationToken.None);

        var next = Command(Guid.NewGuid(), "workspace");
        var dispatch = store.DispatchAsync(next, TimeSpan.FromSeconds(1), CancellationToken.None);

        var pending = await store.TryDequeueAsync(["workspace"], TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.That(pending, Is.EqualTo(next));

        store.CompleteCommand(new BrowserCommandResultDto(next.CommandId, true, null, null));
        await dispatch;
    }

    private static BrowserCommandDto Command(Guid id, string workspaceId) =>
        new(id, workspaceId, BrowserCommandKind.Click, null, "#save", null, null, 100);
}
