using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceOverviewService(
    WorkspaceQueryController workspaces,
    ProcessesController processes,
    IWorkspaceDiskUsageProvider disk)
{
    public WorkspaceOverviewDto? Get(string id)
    {
        var workspace = workspaces.GetById(id);
        if (workspace is null)
            return null;

        var runtime = processes.GetRuntime(id);
        var storageBytes = disk.Measure(workspace.WorktreePath);
        return new WorkspaceOverviewDto(
            workspace.Id,
            workspace.DisplayName,
            workspace.RepositoryPath,
            workspace.WorktreePath,
            workspace.Branch,
            workspace.Commit,
            workspace.State.ToString(),
            runtime.CpuPercent,
            runtime.MemoryBytes,
            storageBytes,
            runtime.ProcessCount,
            workspace.Applications.Count);
    }
}
