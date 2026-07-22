using System.ComponentModel;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Mcp.Services;

public sealed class McpWorkspaceService
{
    private const string MissingConfigurationGuidance =
        "agent-up.json was not found. Inspect docs/user-docs/agent-up-json.md, search the repository for an existing agent-up.json, or ask the user before creating one.";

    private readonly WorkspaceRegistry _registry;
    private readonly ProcessesController _processes;
    private readonly IAgentUpConfigurationProvider _configuration;
    private readonly IWorkspaceIdentityProvider _identity;

    public McpWorkspaceService(
        WorkspaceRegistry registry,
        ProcessesController processes,
        IAgentUpConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity)
    {
        _registry = registry;
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
        var workspace = await _registry.RegisterAsync(new RegisterWorkspaceRequest(
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
            Data = _registry.GetById(workspace.Id)
        };
    }

    public async Task<McpToolResult> StopAsync(string? id, string? worktreePath)
    {
        var workspace = ResolveWorkspace(id, worktreePath);
        if (workspace is null)
            return new McpToolResult(false, "Workspace not found.");

        await _registry.UpdateStateAsync(workspace.Id, WorkspaceState.Stopping);
        foreach (var app in workspace.Applications)
            await _registry.UpdateApplicationStateAsync(workspace.Id, app.Name, ApplicationState.Stopping);

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

        await _registry.UpdateStateAsync(workspace.Id, WorkspaceState.Stopped);
        foreach (var app in workspace.Applications)
            await _registry.UpdateApplicationStateAsync(workspace.Id, app.Name, ApplicationState.Stopped);

        return new McpToolResult(true, $"Stopped workspace \"{workspace.DisplayName}\".", _registry.GetById(workspace.Id));
    }

    public McpToolResult GetStatus(string? id, string? worktreePath)
    {
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(true, "Returned all workspaces.", _registry.GetAll());

        var workspace = ResolveWorkspace(id, worktreePath);
        return workspace is null
            ? new McpToolResult(false, "Workspace not found.")
            : new McpToolResult(true, "Returned workspace status.", workspace);
    }

    public IReadOnlyList<Workspace> List() => _registry.GetAll();

    public Workspace? GetById(string id) => _registry.GetById(id);

    private async Task<McpToolResult> StartRegisteredAsync(string id)
    {
        var workspace = _registry.GetById(id);
        if (workspace is null)
            return new McpToolResult(false, "Workspace not found.");

        await _registry.UpdateStateAsync(id, WorkspaceState.Starting);
        foreach (var app in workspace.Applications)
            await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Starting);

        try
        {
            await _registry.ReallocatePortsAsync(id);
            await _processes.LaunchWorkspaceAsync(workspace);
            await _registry.UpdateStateAsync(id, WorkspaceState.Running);
            foreach (var app in workspace.Applications)
                await _registry.UpdateApplicationStateAsync(id, app.Name, ApplicationState.Running);

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
        await _registry.UpdateStateAsync(workspaceId, WorkspaceState.Failed);
        return new McpToolResult(false, ex.Message, _registry.GetById(workspaceId));
    }

    private Workspace? ResolveWorkspace(string? id, string? worktreePath)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return _registry.GetById(id);

        if (string.IsNullOrWhiteSpace(worktreePath))
            return null;

        return _registry.GetAll().FirstOrDefault(w =>
            string.Equals(w.WorktreePath, worktreePath, StringComparison.OrdinalIgnoreCase));
    }
}
