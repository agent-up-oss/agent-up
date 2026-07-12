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
            foreach (var app in workspace.Applications)
                app.State = ApplicationState.Stopped;
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
        var existing = _workspaces.Values.FirstOrDefault(w =>
            string.Equals(w.WorktreePath, request.WorktreePath, StringComparison.OrdinalIgnoreCase));

        var workspace = new Workspace
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString(),
            DisplayName = request.DisplayName,
            RepositoryPath = request.RepositoryPath,
            WorktreePath = request.WorktreePath,
            Branch = request.Branch,
            Commit = request.Commit,
            State = WorkspaceState.Stopped,
            Applications = request.Applications
                .Select(d => new ApplicationInstance
                {
                    Name = d.Name,
                    Command = d.Command,
                    Path = d.Path,
                    PortVariable = d.PortVariable
                })
                .Concat(request.Services.Select(s => new ApplicationInstance
                {
                    Name = s.Name,
                    ServiceType = ServiceType.Docker,
                    Image = s.Image,
                    Ports = s.Ports,
                    Environment = s.Environment,
                    Volumes = s.Volumes
                }))
                .ToList()
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

    public async Task<bool> UpdateApplicationStateAsync(string workspaceId, string appName, ApplicationState state)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
            return false;

        var app = workspace.Applications.FirstOrDefault(a => a.Name == appName);
        if (app is null)
            return false;

        app.State = state;
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
