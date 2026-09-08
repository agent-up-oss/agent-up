using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.SourceClones.DTOs;

public sealed record SourceCloneResult(bool Succeeded, string? Error, Workspace? Workspace)
{
    public static SourceCloneResult Success(Workspace workspace) => new(true, null, workspace);

    public static SourceCloneResult Failed(string error) => new(false, error, null);
}
