using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

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
