using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Fake;

internal sealed class PartialFailWorkspaceProcessManager : IWorkspaceProcessManager
{
    public int KillCount { get; private set; }

    public Task LaunchAsync(Workspace workspace)
    {
        var last = workspace.Applications.LastOrDefault();
        if (last is not null)
            last.State = ApplicationState.Failed;
        return Task.CompletedTask;
    }

    public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;

    public Task KillAsync(string workspaceId)
    {
        KillCount++;
        return Task.CompletedTask;
    }

    public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
}
