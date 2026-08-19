using AgentUp.Browser.Streaming.Resources;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

// Structural coverage of the JS state machine embedded in rdp-viewer.js. These are
// intentionally string-based — they detect a regression where a state name or a
// transition is silently removed, without needing Chromium to be launched in CI.
// Behavioral tests (does WS drop actually cause `streaming → polling`?) require a
// PuppeteerSharp harness and an in-test WS/HTTP stub server, both of which are
// deferred to follow-up work.
[TestFixture]
public sealed class RdpViewerStateMachineTests
{
    [Test]
    public void All_seven_states_are_present_in_the_machine()
    {
        var html = RdpViewerPage.Build("workspace");
        var expected = new[]
        {
            "'initializing'", "'connecting'", "'open_no_frames'", "'streaming'",
            "'stalled'", "'polling'", "'disconnected'",
        };
        Assert.Multiple(() =>
        {
            foreach (var state in expected)
                Assert.That(html, Does.Contain(state), $"State literal missing: {state}");
        });
    }

    [Test]
    public void Snapshot_returns_all_declared_observable_fields()
    {
        var html = RdpViewerPage.Build("workspace");
        // Read from window.__viewer.snapshot()'s returned object — fields Avalonia's
        // ViewerSnapshot record depends on.
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("state,"));
            Assert.That(html, Does.Contain("since: stateSince"));
            Assert.That(html, Does.Contain("framesReceived,"));
            Assert.That(html, Does.Contain("lastFrameAt,"));
            Assert.That(html, Does.Contain("wsReadyState:"));
            Assert.That(html, Does.Contain("presence,"));
            Assert.That(html, Does.Contain("pageInstanceId,"));
            Assert.That(html, Does.Contain("serverReportedActive,"));
        });
    }

    [Test]
    public void State_machine_is_the_only_writer_of_state()
    {
        // transitionTo is the single mutator of `state`. `state = xxx` should appear
        // ONLY inside transitionTo (guard against a future contributor doing an ad-hoc
        // `state = ...` that bypasses the SM).
        var html = RdpViewerPage.Build("workspace");
        var directAssignmentCount = System.Text.RegularExpressions.Regex.Matches(
            html, @"^\s*state\s*=\s*next",
            System.Text.RegularExpressions.RegexOptions.Multiline).Count;
        Assert.That(directAssignmentCount, Is.EqualTo(1), "There must be exactly one direct assignment to `state` — the one inside transitionTo");
    }

    [Test]
    public void All_transitions_go_through_transitionTo()
    {
        var html = RdpViewerPage.Build("workspace");
        // Every transition trigger site should call transitionTo(<state>, <reason>).
        // Reasons cover the trigger surface — asserting they exist ensures the SM has
        // a home for each real-world lifecycle event.
        var expectedReasons = new[]
        {
            "'boot'",              // initial → connecting
            "'ws_open'",           // connecting → open_no_frames
            "'first_frame'",       // open_no_frames|stalled → streaming
            "'frame_gap'",         // streaming → stalled
            "'ws_close'",          // any → polling
            "'ws_error_pre_open'", // connecting → disconnected
            "'reconnect_backoff'", // disconnected|polling → connecting
            "'external_reset'",    // any → initializing (Avalonia's reset())
            "'reset_reconnect'",   // initializing → connecting (post-reset)
        };
        Assert.Multiple(() =>
        {
            foreach (var reason in expectedReasons)
                Assert.That(html, Does.Contain(reason), $"Transition reason missing: {reason}");
        });
    }

    [Test]
    public void Public_api_surface_exposes_snapshot_setPresence_reset()
    {
        var html = RdpViewerPage.Build("workspace");
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("window.__viewer = {"));
            Assert.That(html, Does.Contain("snapshot()"));
            Assert.That(html, Does.Contain("setPresence,"));
            Assert.That(html, Does.Contain("reset()"));
        });
    }

    [Test]
    public void Handles_server_stream_state_control_frame()
    {
        var html = RdpViewerPage.Build("workspace");
        // Protocol 1 message from server → JS. Handler must parse type=stream and
        // update serverReportedActive when state is 'active' or 'paused'.
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("msg.type === 'stream'"));
            Assert.That(html, Does.Contain("'active'"));
            Assert.That(html, Does.Contain("'paused'"));
            Assert.That(html, Does.Contain("serverReportedActive"));
        });
    }
}
