using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class NullWorkspaceProcessManager : IWorkspaceProcessManager
{
    public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
    public Task LaunchApplicationAsync(Workspace workspace, string appName) => Task.CompletedTask;
    public Task KillAsync(string workspaceId) => Task.CompletedTask;
    public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
}
