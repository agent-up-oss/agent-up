using AgentUp.Browser.Streaming;

namespace AgentUp.Browser.Streaming.Tests.Fake;

internal sealed class FakeStreamSessionEventSink : IStreamSessionEventSink
{
    public void OnChromiumStateChanged(string state, int progress)
    {
    }

    public void OnSessionActive(string workspaceId)
    {
    }

    public void OnSessionInactive(string workspaceId)
    {
    }

    public void OnCurrentTargetChanged(string workspaceId, string url, CancellationToken ct)
    {
    }
}
