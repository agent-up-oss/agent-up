namespace AgentUp.Server.Features.Workspaces.DTOs;

public sealed record WorkspaceOverviewDto(
    string Id,
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit,
    string State,
    double CpuPercent,
    long MemoryBytes,
    long StorageBytes,
    int ProcessCount,
    int ApplicationCount);
