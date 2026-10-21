using AgentUp.Server.Features.Applications.DTOs;

namespace AgentUp.Server.Features.Workspaces.DTOs;

public class Workspace
{
    public required string Id { get; init; }
    public required string DisplayName { get; set; }
    public required string RepositoryPath { get; init; }
    public required string WorktreePath { get; init; }
    public required string Branch { get; set; }
    public required string Commit { get; set; }
    public bool Derived { get; init; }
    public string? SourceRepository { get; init; }
    public string? SourceRevision { get; set; }
    public WorkspaceState State { get; set; } = WorkspaceState.Stopped;
    /// <summary>Enabled agent module id bound to this workspace. Null means none is chosen yet.</summary>
    public string? BoundAgentModuleId { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset LastActivityAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ApplicationInstance> Applications { get; init; } = [];
}
