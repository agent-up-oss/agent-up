using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Features.Workspaces.Models;

public sealed record WorkspaceConfigurationResult(AgentUpJson? Configuration, string? WorkspaceRoot, string? Error)
{
    public bool Succeeded => Configuration is not null && WorkspaceRoot is not null;
}
