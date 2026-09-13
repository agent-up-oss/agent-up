using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming.Tests.Features.Sessions.Unit;

[TestFixture]
public sealed class BrowserSessionStoreSupersessionTests
{
    [Test]
    public void NewNavigationCancelsOnlyThePreviousNavigationForItsWorkspace()
    {
        var store = new BrowserSessionStore();
        var first = Navigate("workspace");
        var otherWorkspace = Navigate("other");
        _ = store.DispatchAsync(first, TimeSpan.FromSeconds(1), CancellationToken.None);
        _ = store.DispatchAsync(otherWorkspace, TimeSpan.FromSeconds(1), CancellationToken.None);
        var firstToken = store.SupersessionToken(first);
        var otherToken = store.SupersessionToken(otherWorkspace);

        var latest = Navigate("workspace");
        _ = store.DispatchAsync(latest, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(firstToken.IsCancellationRequested, Is.True);
            Assert.That(store.SupersessionToken(first).IsCancellationRequested, Is.True);
            Assert.That(store.SupersessionToken(latest).IsCancellationRequested, Is.False);
            Assert.That(otherToken.IsCancellationRequested, Is.False);
        });
    }

    [Test]
    public async Task CallerCancellationCancelsNavigationExecution()
    {
        var store = new BrowserSessionStore();
        var command = Navigate("workspace");
        using var caller = new CancellationTokenSource();
        var dispatch = store.DispatchAsync(command, TimeSpan.FromSeconds(1), caller.Token);
        var execution = store.SupersessionToken(command);

        await caller.CancelAsync();
        var result = await dispatch;

        Assert.Multiple(() =>
        {
            Assert.That(execution.IsCancellationRequested, Is.True);
            Assert.That(result.Error, Is.EqualTo("Request was cancelled."));
        });
    }

    private static BrowserCommandDto Navigate(string workspaceId) =>
        new(Guid.NewGuid(), workspaceId, BrowserCommandKind.Navigate, "http://127.0.0.1:5000", null, null, null, 100);
}
