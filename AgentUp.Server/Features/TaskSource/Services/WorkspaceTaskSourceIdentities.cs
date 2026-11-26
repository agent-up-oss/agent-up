using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.TaskSource.Services;

/// <summary>Every workspace on this Server is one identity that can hold its own access token.</summary>
public sealed class WorkspaceTaskSourceIdentities(WorkspaceQueryController workspaces) : ITaskSourceIdentities
{
    public IReadOnlyList<TaskSourcePerson> List()
        => workspaces.GetAll().Select(workspace => new TaskSourcePerson(workspace.Id, workspace.DisplayName)).ToArray();
}
