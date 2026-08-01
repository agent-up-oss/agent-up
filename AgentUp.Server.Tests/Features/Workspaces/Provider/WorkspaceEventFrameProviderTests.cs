using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Providers;

namespace AgentUp.Server.Tests.Features.Workspaces.Provider;

[TestFixture]
public sealed class WorkspaceEventFrameProviderTests
{
    [Test]
    public void Format_ReturnsServerSentEventFrameWithCamelCaseJson()
    {
        var provider = new WorkspaceEventFrameProvider();
        var evt = new WorkspaceStateChangedEvent("ws-1", "Running", [new AppStateChange("Web", "Running")]);

        var frame = provider.Format(evt);

        Assert.That(frame, Is.EqualTo(
            "data: {\"workspaceId\":\"ws-1\",\"state\":\"Running\",\"applications\":[{\"name\":\"Web\",\"state\":\"Running\"}]}\n\n"));
    }
}
