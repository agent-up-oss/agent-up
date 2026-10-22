namespace AgentUp.Server.Features.Workspaces.DTOs;

public enum WorkspaceState
{
    Stopped,
    Starting,
    Running,
    Degraded,
    Stopping,
    Failed
}
