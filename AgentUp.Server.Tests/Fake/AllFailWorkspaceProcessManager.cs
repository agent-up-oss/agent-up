using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Fake;

internal sealed class AllFailWorkspaceProcessManager : IWorkspaceProcessManager
{
    public int KillCount { get; private set; }

    public Task LaunchAsync(Workspace workspace)
    {
        foreach (var app in workspace.Applications)
            app.State = ApplicationState.Failed;
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
