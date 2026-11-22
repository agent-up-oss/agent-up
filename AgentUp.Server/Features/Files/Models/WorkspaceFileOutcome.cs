namespace AgentUp.Server.Features.Files.Models;

public enum WorkspaceFileOutcome
{
    Succeeded,
    WorkspaceNotFound,
    FileNotFound,
    Invalid,
    Stale,
    Busy
}
