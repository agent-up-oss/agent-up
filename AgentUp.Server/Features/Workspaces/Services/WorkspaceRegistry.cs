using System.Collections.Concurrent;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceRegistry : IWorkspaceRegistry, IHostedService
{
    private readonly ConcurrentDictionary<string, Workspace> _workspaces = new();
    private readonly IWorkspaceRepository _repository;

    public WorkspaceRegistry(IWorkspaceRepository repository)
    {
        _repository = repository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var persisted = await _repository.LoadAllAsync(cancellationToken);
        foreach (var workspace in persisted)
        {
            workspace.State = WorkspaceState.Stopped;
            _workspaces[workspace.Id] = workspace;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IReadOnlyList<Workspace> GetAll() =>
        _workspaces.Values.OrderBy(w => w.DisplayName).ToList();

    public Workspace? GetById(string id) =>
        _workspaces.GetValueOrDefault(id);

    public async Task<Workspace> RegisterAsync(RegisterWorkspaceRequest request)
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
        await _repository.SaveAllAsync(GetAll());
        return workspace;
    }

    public async Task<bool> UpdateStateAsync(string id, WorkspaceState state)
    {
        if (!_workspaces.TryGetValue(id, out var workspace))
            return false;

        workspace.State = state;
        await _repository.SaveAllAsync(GetAll());
        return true;
    }

    public async Task<bool> RemoveAsync(string id)
    {
        if (!_workspaces.TryRemove(id, out _))
            return false;

        await _repository.SaveAllAsync(GetAll());
        return true;
    }
}
