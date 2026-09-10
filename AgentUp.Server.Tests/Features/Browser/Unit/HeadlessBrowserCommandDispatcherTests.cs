using AgentUp.Browser.Streaming;
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

    [Test]
    public async Task StopAsync_StopsTheNewLoop_WhenTheServiceIsRestarted()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.StartAsync(CancellationToken.None);
        await dispatcher.StopAsync(CancellationToken.None);

        await dispatcher.StartAsync(CancellationToken.None);

        Assert.DoesNotThrowAsync(async () => await dispatcher.StopAsync(CancellationToken.None));
    }

    // The dispatcher only reaches for the session manager and executor while draining queued
    // commands, and these lifecycle tests never queue one, so an empty store is enough. The
    // session manager is never started either, so its directory arguments stay untouched names.
    private static HeadlessBrowserCommandDispatcher CreateDispatcher()
    {
        var display = new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance);
        var streamState = new FakeStreamSessionEventSink();
        var sessions = new HeadlessBrowserSessionManager(
            "chromium", "profiles", display,
            streamState,
            NullLogger<HeadlessBrowserSessionManager>.Instance);
        var executor = new CdpBrowserExecutor(streamState, NullLogger<CdpBrowserExecutor>.Instance);

        return new HeadlessBrowserCommandDispatcher(
            new BrowserSessionStore(),
            sessions,
            executor,
            NullLogger<HeadlessBrowserCommandDispatcher>.Instance);
    }

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
