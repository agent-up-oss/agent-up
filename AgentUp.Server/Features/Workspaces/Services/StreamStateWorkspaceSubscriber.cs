using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Workspaces.Models;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

// Forwards workspace-removal events to the browser stream-state machine so its
// cache doesn't outlive the workspace. Stops are handled inline in
// WorkspaceLifecycleService; removals happen outside the lifecycle service
// (WorkspacesController.Delete + tutorial cleanup), so we listen on the shared bus.
public sealed class StreamStateWorkspaceSubscriber(
    WorkspaceEventBus workspaceEvents,
    WorkspaceStreamStateController streamState) : BackgroundService
{
    private const string RemovedState = "Removed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var sub = workspaceEvents.Subscribe();
        await foreach (var evt in sub.Reader.ReadAllAsync(stoppingToken))
            Handle(evt);
    }

    private void Handle(WorkspaceStateChangedEvent evt)
    {
        if (!string.Equals(evt.State, RemovedState, StringComparison.Ordinal)) return;
        streamState.OnWorkspaceRemoved(evt.WorkspaceId);
    }
}
