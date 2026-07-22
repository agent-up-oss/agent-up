using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Ports.Models;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceRegistry : IHostedService
{
    private readonly ConcurrentDictionary<string, Workspace> _workspaces = new();
    private readonly IWorkspaceRepository _repository;
    private readonly PortsController _ports;
    private readonly CapabilitiesController _capabilities;

    public WorkspaceRegistry(
        IWorkspaceRepository repository,
        PortsController ports,
        CapabilitiesController capabilities)
    {
        _repository = repository;
        _ports = ports;
        _capabilities = capabilities;
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

        var workspaceId = existing?.Id ?? Guid.NewGuid().ToString();

        var basePort = await _ports.GetBasePortAsync(workspaceId);
        var portCounter = basePort;

        IReadOnlyList<PortMapping> AllocatePorts(IReadOnlyList<PortDeclaration>? declarations) =>
            (declarations ?? []).Select(d => new PortMapping(d.Variable, d.DefaultPort, portCounter++, d.Protocol)).ToList();

        var typedDotnetApplications = new List<ApplicationInstance>();
        foreach (var dotnet in request.Dotnet)
        {
            var ports = dotnet.Ports ?? [];
            typedDotnetApplications.Add(await _capabilities.ReconcileDotnetAsync(dotnet, ports, AllocatePorts(ports)));
        }

        var typedDockerApplications = new List<ApplicationInstance>();
        foreach (var docker in request.Docker)
        {
            var ports = docker.Ports ?? [];
            typedDockerApplications.Add(await _capabilities.ReconcileDockerAsync(docker, ports, AllocatePorts(ports)));
        }

        var workspace = new Workspace
        {
            Id = workspaceId,
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
                    Environment = d.Environment,
                    EnvironmentFiles = d.EnvironmentFiles,
                    Ports = d.Ports ?? [],
                    AllocatedPorts = AllocatePorts(d.Ports)
                })
                .Concat(request.Services.Select(s => new ApplicationInstance
                {
                    Name = s.Name,
                    ServiceType = ServiceType.Docker,
                    Image = s.Image,
                    Ports = s.Ports ?? [],
                    AllocatedPorts = AllocatePorts(s.Ports),
                    Environment = s.Environment,
                    EnvironmentFiles = s.EnvironmentFiles,
                    Volumes = s.Volumes
                }))
                .Concat(typedDotnetApplications)
                .Concat(typedDockerApplications)
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

    public async Task ReallocatePortsAsync(string workspaceId)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
            return;

        var portCount = workspace.Applications.Sum(a => a.Ports.Count);
        var basePort = await _ports.GetConflictFreeBasePortAsync(workspaceId, portCount);
        var portCounter = basePort;

        foreach (var app in workspace.Applications)
            app.AllocatedPorts = app.Ports
                .Select(p => new PortMapping(p.Variable, p.DefaultPort, portCounter++, p.Protocol))
                .ToList();

        await _repository.SaveAllAsync(GetAll());
    }

    public async Task<bool> RemoveAsync(string id)
    {
        if (!_workspaces.TryRemove(id, out _))
            return false;

        await _ports.ReleaseAsync(id);
        await _repository.SaveAllAsync(GetAll());
        return true;
    }
}
