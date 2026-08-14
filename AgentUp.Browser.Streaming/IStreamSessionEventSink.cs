namespace AgentUp.Browser.Streaming;

// Decouples HeadlessBrowserSessionManager and CdpBrowserExecutor from
// WorkspaceStreamStateService. Implement this in the host application to receive
// streaming lifecycle events and update stream-state derivation accordingly.
public interface IStreamSessionEventSink
{
    void OnChromiumStateChanged(string state, int progress);
    void OnSessionActive(string workspaceId);
    void OnSessionInactive(string workspaceId);
    void OnCurrentTargetChanged(string workspaceId, string url, CancellationToken ct);
}
