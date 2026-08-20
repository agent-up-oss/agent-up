using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Browser.Controllers;

// Cross-slice entry point for workspace-lifecycle transitions that must update the
// browser stream-state machine. Sibling slices (Workspaces) call these methods
// rather than depending on WorkspaceStreamStateService directly.
public sealed class WorkspaceStreamStateController(WorkspaceStreamStateService streamState)
{
    public void OnWorkspaceStarted(Workspace workspace) => streamState.OnWorkspaceStarted(workspace);

    public void OnWorkspaceStopped(string workspaceId) => streamState.OnWorkspaceStopped(workspaceId);

    public void OnWorkspaceRemoved(string workspaceId) => streamState.OnWorkspaceRemoved(workspaceId);
}
