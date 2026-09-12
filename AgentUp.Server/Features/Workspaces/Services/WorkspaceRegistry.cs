using System.Collections.Concurrent;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Providers;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed class WorkspaceRegistry : IHostedService
{
    public event Action<string>? WorkspaceRemoved;
    private readonly ConcurrentDictionary<string, Workspace> _workspaces = new();
    private readonly IWorkspaceRepository _repository;
    private readonly PortsController _ports;
    private readonly CapabilitiesController _capabilities;
    private readonly WorkspaceEventBus _bus;

    public WorkspaceRegistry(
        IWorkspaceRepository repository,
        PortsController ports,
        CapabilitiesController capabilities,
        WorkspaceEventBus bus)
    {
        _repository = repository;
        _ports = ports;
        _capabilities = capabilities;
        _bus = bus;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var persisted = await _repository.LoadAllAsync(cancellationToken);
        foreach (var workspace in persisted)
        {
            workspace.State = WorkspaceState.Stopped;
            workspace.LastError = null;
            if (workspace.LastActivityAtUtc == default)
                workspace.LastActivityAtUtc = DateTimeOffset.UtcNow;
            foreach (var app in workspace.Applications)
                app.State = ApplicationState.Stopped;
            _workspaces[workspace.Id] = workspace;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IReadOnlyList<Workspace> GetAll() =>
        _workspaces.Values
            .OrderByDescending(w => WorkspaceListOrdering.ActivePriority(w.State))
            .ThenByDescending(w => w.LastActivityAtUtc)
            .ThenBy(w => w.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
            (declarations ?? []).Select(d => new PortMapping(d.Variable, d.DefaultPort, portCounter++, d.Protocol, d.HealthCheckPath, d.MetricsPath)).ToList();

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
            LastActivityAtUtc = DateTimeOffset.UtcNow,
            Applications = request.Applications
                .Select(d => new ApplicationInstance
                {
                    Name = d.Name,
                    Command = d.Command,
                    Install = d.Install,
                    Path = d.Path,
                    Environment = d.Environment,
                    EnvironmentFiles = d.EnvironmentFiles,
                    Ports = d.Ports ?? [],
                    AllocatedPorts = AllocatePorts(d.Ports),
                    Database = d.Database
                })
                .Concat(request.DesktopApplications.Select(d => new ApplicationInstance
                {
                    Name = d.Name,
                    Kind = ApplicationKind.Desktop,
                    Command = d.Command,
                    Install = d.Install,
                    Path = d.Path,
                    Environment = d.Environment,
                    EnvironmentFiles = d.EnvironmentFiles,
                    Ports = d.Ports ?? [],
                    AllocatedPorts = AllocatePorts(d.Ports),
                    DesktopWidth = ValidateDesktopDimension(d.Window?.Width ?? 1280, 320, 3840, "width"),
                    DesktopHeight = ValidateDesktopDimension(d.Window?.Height ?? 800, 240, 2160, "height"),
                    DesktopRuntime = ValidateDesktopRuntime(d.Runtime)
                }))
                .Concat(request.Services.Select(s => new ApplicationInstance
                {
                    Name = s.Name,
                    ServiceType = ServiceType.Docker,
                    Image = s.Image,
                    Ports = s.Ports ?? [],
                    AllocatedPorts = AllocatePorts(s.Ports),
                    Environment = s.Environment,
                    EnvironmentFiles = s.EnvironmentFiles,
                    Volumes = s.Volumes,
                    Args = s.Command,
                    Database = s.Database
                }))
                .Concat(typedDotnetApplications)
                .Concat(typedDockerApplications)
                .ToList()
        };

        WorkspaceEnvironmentFilesValidator.Validate(
            request.WorktreePath,
            workspace.Applications.Select(app => new ApplicationEnvironmentFileSource(
                app.Name,
                app.EnvironmentFiles)));

        _workspaces[workspace.Id] = workspace;
        await _repository.SaveAllAsync(GetAll());
        _bus.PublishWorkspaceChange(workspace);
        return workspace;
    }

    public async Task<bool> UpdateStateAsync(string id, WorkspaceState state)
    {
        if (!_workspaces.TryGetValue(id, out var workspace))
            return false;

        workspace.State = state;
        TouchActivity(workspace);
        await _repository.SaveAllAsync(GetAll());
        _bus.PublishWorkspaceChange(workspace);
        return true;
    }

    public async Task UpdateLastErrorAsync(string id, string? error)
    {
        if (!_workspaces.TryGetValue(id, out var workspace))
            return;

        workspace.LastError = error;
        await _repository.SaveAllAsync(GetAll());
    }

    public async Task<bool> UpdateApplicationStateAsync(string workspaceId, string appName, ApplicationState state)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
            return false;

        var app = workspace.Applications.FirstOrDefault(a => a.Name == appName);
        if (app is null)
            return false;

        app.State = state;
        TouchActivity(workspace);
        await _repository.SaveAllAsync(GetAll());
        _bus.PublishWorkspaceChange(workspace);
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
                .Select(p => new PortMapping(p.Variable, p.DefaultPort, portCounter++, p.Protocol, p.HealthCheckPath, p.MetricsPath))
                .ToList();

        await _repository.SaveAllAsync(GetAll());
    }

    public async Task<bool> RemoveAsync(string id)
    {
        if (!_workspaces.TryRemove(id, out _))
            return false;

        await _ports.ReleaseAsync(id);
        await _repository.SaveAllAsync(GetAll());
        _bus.Publish(new WorkspaceStateChangedEvent(id, "Removed", []));
        WorkspaceRemoved?.Invoke(id);
        return true;
    }

    private static void TouchActivity(Workspace workspace) =>
        workspace.LastActivityAtUtc = DateTimeOffset.UtcNow;

    private static string ValidateDesktopRuntime(string runtime)
    {
        if (!string.Equals(runtime, "linux", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Desktop application runtime must currently be 'linux'.");
        return "linux";
    }

    private static int ValidateDesktopDimension(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
            throw new InvalidOperationException($"Desktop application window {name} must be between {minimum} and {maximum}.");
        return value;
    }
}
