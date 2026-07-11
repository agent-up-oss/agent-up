using System.Collections.Concurrent;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

public class WorkspaceRegistry : IWorkspaceRegistry
{
    private readonly ConcurrentDictionary<string, Workspace> _workspaces = new();

    public IReadOnlyList<Workspace> GetAll() =>
        _workspaces.Values.OrderBy(w => w.DisplayName).ToList();

    public Workspace? GetById(string id) =>
        _workspaces.GetValueOrDefault(id);

    public Workspace Register(RegisterWorkspaceRequest request)
    {
        var workspace = new Workspace
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = request.DisplayName,
            RepositoryPath = request.RepositoryPath,
            WorktreePath = request.WorktreePath,
            Branch = request.Branch,
            Commit = request.Commit,
            State = WorkspaceState.Stopped
        };

        _workspaces[workspace.Id] = workspace;
        return workspace;
    }

    public bool UpdateState(string id, WorkspaceState state)
    {
        if (!_workspaces.TryGetValue(id, out var workspace))
            return false;

        workspace.State = state;
        return true;
    }

    public bool Remove(string id) =>
        _workspaces.TryRemove(id, out _);
}
