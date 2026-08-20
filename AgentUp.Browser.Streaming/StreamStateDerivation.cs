using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming;

// Pure derivation function extracted from WorkspaceStreamStateService.Compute().
// Takes all inputs explicitly — no instance state, no side effects.
// Call this from the host application's stream-state coordinator after updating inputs.
public static class StreamStateDerivation
{
    public const int StandaloneMaxAttempts = 30;

    public static StreamState Compute(
        WorkspaceStreamInputs inputs,
        string chromiumState,
        int chromiumProgress)
    {
        // 1. Chromium precedence: if the binary isn't ready, nothing else matters.
        if (chromiumState is "not_started" or "downloading" or "failed")
            return StreamState.Chromium(chromiumState, chromiumProgress);

        // 2. Workspace lifecycle.
        if (!inputs.IsRunning) return StreamState.Stopped();

        // 3. App reachability.
        if (inputs.CurrentTarget is null)
        {
            // Waiting for the desktop to navigate to a URL. Show a benign "connecting" so
            // the UI never renders a bare WebView while we have no target.
            return StreamState.Connecting(attempt: 0, maxAttempts: 0);
        }
        if (inputs.CurrentTarget is { HealthChecked: true, AppName: { } appName } target)
        {
            var key = HealthKey(appName, target.Port);
            var state = inputs.PortHealth.GetValueOrDefault(key, "Checking");
            if (state != "Healthy") return StreamState.Connecting(attempt: 0, maxAttempts: 0);
        }
        else
        {
            // Standalone (non-health-checked) target: probe state is authoritative. Compute
            // reads the probe's last-observed fields so the probe cannot bypass IsRunning
            // above and leak stale AppConnecting after a workspace stop.
            if (inputs.StandaloneProbeExhausted)
                return StreamState.Failed($"App unreachable after {StandaloneMaxAttempts} attempts.", StandaloneMaxAttempts);
            if (!inputs.StandaloneProbeReachable)
                return StreamState.Connecting(inputs.StandaloneProbeAttempt, StandaloneMaxAttempts);
        }

        // 4. Session liveness. Session must exist server-side — otherwise the viewer HTML
        //    page opens a WebSocket to nothing. First-frame liveness is intentionally NOT
        //    tracked here: the RDP display loop only broadcasts frames when a subscriber
        //    exists, and the viewer HTML page only subscribes once the desktop shows the
        //    WebView. Gating Streaming on "first frame received" would deadlock. The viewer
        //    HTML has its own "connecting…" spinner shown until the first frame lands, so
        //    the "bare WebView" invariant is upheld by the viewer page itself, not by us.
        if (!inputs.SessionActive) return StreamState.Launching();

        return StreamState.Streaming();
    }

    public static string HealthKey(string appName, int port) => $"{appName}:{port}";
}
