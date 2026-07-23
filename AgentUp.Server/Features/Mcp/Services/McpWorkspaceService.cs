using System.ComponentModel;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Mcp.Services;

public sealed class McpWorkspaceService
{
    private const string MissingConfigurationGuidance =
        "agent-up.json was not found. Inspect docs/user-docs/agent-up-json.md, search the repository for an existing agent-up.json, or ask the user before creating one.";

    private readonly WorkspaceQueryController _workspaces;
    private readonly WorkspaceStateController _states;
    private readonly ProcessesController _processes;
    private readonly IAgentUpConfigurationProvider _configuration;
    private readonly IWorkspaceIdentityProvider _identity;

    public McpWorkspaceService(
        WorkspaceQueryController workspaces,
        WorkspaceStateController states,
        ProcessesController processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
    {
        _workspaces = workspaces;
        _states = states;
        _processes = processes;
        _configuration = configuration;
        _identity = identity;
    }

    public async Task<McpToolResult> StartAsync(string worktreePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        AgentUpConfiguration? config;
        try
        {
            config = await _configuration.LoadAsync(worktreePath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException ex)
        {
            return CreateConfigurationReadFailure(ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            return CreateConfigurationReadFailure(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateConfigurationReadFailure(ex);
        }
        catch (IOException ex)
        {
            return CreateConfigurationReadFailure(ex);
        }
        catch (InvalidOperationException ex)
        {
            return CreateConfigurationReadFailure(ex);
        }

        if (config is null)
            return new McpToolResult(false, MissingConfigurationGuidance);

        var identity = await _identity.ReadAsync(worktreePath, cancellationToken);
        var workspace = await _workspaces.RegisterAsync(new RegisterWorkspaceRequest(
            DisplayName: config.Name,
            RepositoryPath: identity.RepositoryPath,
            WorktreePath: worktreePath,
            Branch: identity.Branch,
            Commit: identity.Commit)
        {
            Applications = config.Applications ?? [],
            Services = config.Services ?? []
        });

        var startResult = await StartRegisteredAsync(workspace.Id);
        return startResult with
        {
            Data = _workspaces.GetById(workspace.Id)
        };
    }

    public async Task<McpToolResult> StopAsync(string? id, string? worktreePath)
    {
        var workspace = ResolveWorkspace(id, worktreePath);
        if (workspace is null)
            return new McpToolResult(false, "Workspace not found.");

        await _states.UpdateWorkspaceStateAsync(workspace.Id, WorkspaceState.Stopping);
        foreach (var app in workspace.Applications)
            await _states.UpdateApplicationStateAsync(workspace.Id, app.Name, ApplicationState.Stopping);

        try
        {
            await _processes.KillWorkspaceAsync(workspace.Id);
        }
        catch (InvalidOperationException ex)
        {
            return await CreateWorkspaceOperationFailureAsync(workspace.Id, ex);
        }
        catch (Win32Exception ex)
        {
            return await CreateWorkspaceOperationFailureAsync(workspace.Id, ex);
        }

        await _states.UpdateWorkspaceStateAsync(workspace.Id, WorkspaceState.Stopped);
        foreach (var app in workspace.Applications)
            await _states.UpdateApplicationStateAsync(workspace.Id, app.Name, ApplicationState.Stopped);

        return new McpToolResult(true, $"Stopped workspace \"{workspace.DisplayName}\".", _workspaces.GetById(workspace.Id));
    }

    public McpToolResult GetStatus(string? id, string? worktreePath)
    {
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(true, "Returned all workspaces.", _workspaces.GetAll());

        var workspace = ResolveWorkspace(id, worktreePath);
        return workspace is null
            ? new McpToolResult(false, "Workspace not found.")
            : new McpToolResult(true, "Returned workspace status.", workspace);
    }

    public IReadOnlyList<Workspace> List() => _workspaces.GetAll();

    public Workspace? GetById(string id) => _workspaces.GetById(id);

    private async Task<McpToolResult> StartRegisteredAsync(string id)
    {
        var workspace = _workspaces.GetById(id);
        if (workspace is null)
            return new McpToolResult(false, "Workspace not found.");

        await _states.UpdateWorkspaceStateAsync(id, WorkspaceState.Starting);
        foreach (var app in workspace.Applications)
            await _states.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Starting);

        try
        {
            await _workspaces.ReallocatePortsAsync(id);
            await _processes.LaunchWorkspaceAsync(workspace);
            await _states.UpdateWorkspaceStateAsync(id, WorkspaceState.Running);
            foreach (var app in workspace.Applications)
                await _states.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Running);

            return new McpToolResult(true, $"Started workspace \"{workspace.DisplayName}\".");
        }
        catch (InvalidOperationException ex)
        {
            return await CreateWorkspaceOperationFailureAsync(id, ex);
        }
        catch (IOException ex)
        {
            return await CreateWorkspaceOperationFailureAsync(id, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return await CreateWorkspaceOperationFailureAsync(id, ex);
        }
    }

    private static McpToolResult CreateConfigurationReadFailure(Exception ex) =>
        new(false, $"Failed to read agent-up.json: {ex.Message}");

    private async Task<McpToolResult> CreateWorkspaceOperationFailureAsync(string workspaceId, Exception ex)
    {
        await _states.UpdateWorkspaceStateAsync(workspaceId, WorkspaceState.Failed);
        return new McpToolResult(false, ex.Message, _workspaces.GetById(workspaceId));
    }

    private Workspace? ResolveWorkspace(string? id, string? worktreePath)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return _workspaces.GetById(id);

        if (string.IsNullOrWhiteSpace(worktreePath))
            return null;

        return _workspaces.GetAll().FirstOrDefault(w =>
            string.Equals(w.WorktreePath, worktreePath, StringComparison.OrdinalIgnoreCase));
    }
}
