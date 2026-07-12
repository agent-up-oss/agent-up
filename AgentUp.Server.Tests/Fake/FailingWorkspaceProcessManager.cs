using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FailingWorkspaceProcessManager : IWorkspaceProcessManager
{
    public Task LaunchAsync(Workspace workspace) =>
        throw new InvalidOperationException("No such file or directory");

    public Task LaunchApplicationAsync(Workspace workspace, string appName) =>
        throw new InvalidOperationException("No such file or directory");

    public Task KillAsync(string workspaceId) => Task.CompletedTask;
    public Task KillApplicationAsync(string workspaceId, string appName) => Task.CompletedTask;
}
