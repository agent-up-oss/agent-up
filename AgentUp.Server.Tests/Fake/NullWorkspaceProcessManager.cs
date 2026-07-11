using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Tests.Fake;

internal sealed class NullWorkspaceProcessManager : IWorkspaceProcessManager
{
    public Task LaunchAsync(Workspace workspace) => Task.CompletedTask;
    public Task KillAsync(string workspaceId) => Task.CompletedTask;
}
