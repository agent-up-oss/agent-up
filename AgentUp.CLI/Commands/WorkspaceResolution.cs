using AgentUp.CLI.Http;

namespace AgentUp.CLI.Commands;

public sealed class WorkspaceResolution
{
    private WorkspaceResolution(WorkspaceDto? workspace, string? error)
    {
        Workspace = workspace;
        Error = error;
    }

    public WorkspaceDto? Workspace { get; }
    public string? Error { get; }
    public bool Succeeded => Workspace is not null;

    public static WorkspaceResolution Found(WorkspaceDto workspace)
        => new(workspace, null);

    public static WorkspaceResolution Failed(string error)
        => new(null, error);
}
