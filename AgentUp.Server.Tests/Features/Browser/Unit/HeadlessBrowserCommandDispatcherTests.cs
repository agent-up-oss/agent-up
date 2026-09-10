using AgentUp.Browser.Streaming;
using AgentUp.Browser.Streaming.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class HeadlessBrowserCommandDispatcherTests
{
    // Host shutdown stops a hosted service more than once on some paths — WebApplicationFactory
    // disposal does — and cancelling the already-disposed source failed the whole teardown.
    [Test]
    public async Task StopAsync_IsIdempotent_WhenTheHostStopsTheServiceTwice()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.StartAsync(CancellationToken.None);
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.DoesNotThrowAsync(async () => await dispatcher.StopAsync(CancellationToken.None));
    }

    [Test]
    public void StopAsync_DoesNothing_WhenTheServiceWasNeverStarted()
    {
        var dispatcher = CreateDispatcher();

        Assert.DoesNotThrowAsync(async () => await dispatcher.StopAsync(CancellationToken.None));
    }

    // A restarted host must get a loop it can still stop. "Does not throw" would not show
    // that: without StartAsync resetting the stop guard, the second StopAsync returns at the
    // guard, throws nothing, and leaves the new loop draining commands. So observe the loop
    // instead — a live one answers a queued command, a stopped one lets it time out.
    [Test]
    public async Task StopAsync_StopsTheNewLoop_WhenTheServiceIsRestarted()
    {
        var store = new BrowserSessionStore();
        var dispatcher = CreateDispatcher(store);
        await dispatcher.StartAsync(CancellationToken.None);
        await dispatcher.StopAsync(CancellationToken.None);

        await dispatcher.StartAsync(CancellationToken.None);

        var answered = await store.DispatchAsync(
            Command(), TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.That(answered.Error, Does.Contain("No browser session"),
            "the restarted loop should have drained the queued command");

        await dispatcher.StopAsync(CancellationToken.None);

        var abandoned = await store.DispatchAsync(
            Command(), TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Assert.That(abandoned.Error, Does.Contain("timed out"),
            "the second stop should have cancelled the restarted loop");
    }

    // The session manager is never started, so its directory arguments stay untouched names and
    // GetSession is a plain miss — which is what makes a drained command answer with the
    // no-session failure instead of reaching a real browser.
    private static HeadlessBrowserCommandDispatcher CreateDispatcher() =>
        CreateDispatcher(new BrowserSessionStore());

    private static HeadlessBrowserCommandDispatcher CreateDispatcher(BrowserSessionStore store)
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var streamState = new FakeStreamSessionEventSink();
        var sessions = new HeadlessBrowserSessionManager(
            "chromium", "profiles", display,
            streamState,
            NullLogger<HeadlessBrowserSessionManager>.Instance);
        var executor = new CdpBrowserExecutor(streamState, NullLogger<CdpBrowserExecutor>.Instance);

        return new HeadlessBrowserCommandDispatcher(
            store,
            sessions,
            executor,
            NullLogger<HeadlessBrowserCommandDispatcher>.Instance);
    }

    // Click routes to the session lookup, not to Navigate, so no browser is ever launched.
    private static BrowserCommandDto Command() =>
        new(Guid.NewGuid(), "workspace", BrowserCommandKind.Click, null, "#save", null, null, 100);

    private sealed class FakeStreamSessionEventSink : IStreamSessionEventSink
    {
        public void OnChromiumStateChanged(string state, int progress)
        {
            // Lifecycle-only fixture: session events are irrelevant to start/stop behavior.
        }

        public void OnSessionActive(string workspaceId)
        {
            // Lifecycle-only fixture: session events are irrelevant to start/stop behavior.
        }

        public void OnSessionInactive(string workspaceId)
        {
            // Lifecycle-only fixture: session events are irrelevant to start/stop behavior.
        }

        public void OnCurrentTargetChanged(string workspaceId, string url, CancellationToken ct)
        {
            // Lifecycle-only fixture: session events are irrelevant to start/stop behavior.
        }
    }
}
