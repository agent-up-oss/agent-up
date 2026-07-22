using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Features.Workspaces.Services;

public sealed class WorkspaceCommandService
{
    private readonly WorkspaceApiClient _client;
    private readonly IWorkspaceConfigurationProvider _configuration;
    private readonly IWorkspaceIdentityProvider _identity;
    private readonly CurrentWorkspaceResolver _resolver;
    private readonly string _workingDirectory;

    public WorkspaceCommandService(
        WorkspaceApiClient client,
        IWorkspaceConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity,
        CurrentWorkspaceResolver resolver,
        string workingDirectory)
    {
        _client = client;
        _configuration = configuration;
        _identity = identity;
        _resolver = resolver;
        _workingDirectory = workingDirectory;
    }

    public async Task<WorkspaceCommandResult<StartedWorkspace>> StartAsync()
    {
        var loaded = await _configuration.LoadAsync(_workingDirectory);
        if (!loaded.Succeeded)
            return WorkspaceCommandResult<StartedWorkspace>.Failed(loaded.Error);

        var config = loaded.Configuration!;
        var git = await _identity.ReadAsync(_workingDirectory);
        var applications = config.Applications ?? [];
        var services = config.Services ?? [];
        var dotnet = config.Dotnet ?? [];
        var docker = config.Docker ?? [];

        WorkspaceDto? workspace;
        try
        {
            workspace = await _client.RegisterAsync(new RegisterWorkspaceRequest(
                DisplayName: config.Name,
                RepositoryPath: git.RepositoryPath,
                WorktreePath: _workingDirectory,
                Branch: git.Branch,
                Commit: git.Commit)
            {
                Applications = applications,
                Services = services,
                Dotnet = dotnet,
                Docker = docker
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return WorkspaceCommandResult<StartedWorkspace>.Failed($"Error: Failed to push workspace definition: {ex.Message}");
        }

        if (workspace is null)
            return WorkspaceCommandResult<StartedWorkspace>.Failed("Error: Server returned an unexpected response.");

        try
        {
            await _client.StartWorkspaceAsync(workspace.Id);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return WorkspaceCommandResult<StartedWorkspace>.Failed($"Error: {ex.Message}");
        }

        return WorkspaceCommandResult<StartedWorkspace>.Success(new StartedWorkspace(
            workspace,
            applications,
            services,
            dotnet,
            docker));
    }

    public async Task<WorkspaceCommandResult<IReadOnlyList<WorkspaceDto>>> ListAsync()
    {
        try
        {
            return WorkspaceCommandResult<IReadOnlyList<WorkspaceDto>>.Success(await _client.ListAsync());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return WorkspaceCommandResult<IReadOnlyList<WorkspaceDto>>.Failed($"Error: Failed to list workspaces: {ex.Message}");
        }
    }

    public async Task<WorkspaceResolution> ResolveCurrentAsync(string queryFailureMessage, string missingWorkspaceMessage)
        => await _resolver.ResolveAsync(queryFailureMessage, missingWorkspaceMessage);

    public async Task<WorkspaceCommandResult<WorkspaceDto>> StopCurrentAsync()
    {
        var resolution = await _resolver.ResolveAsync(
            "Error: Failed to connect to server",
            "Error: No workspace found for the current directory. Run 'agent-up start' first.");
        if (!resolution.Succeeded)
            return WorkspaceCommandResult<WorkspaceDto>.Failed(resolution.Error);

        var workspace = resolution.Workspace!;
        try
        {
            await _client.StopWorkspaceAsync(workspace.Id);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return WorkspaceCommandResult<WorkspaceDto>.Failed($"Error: {ex.Message}");
        }

        return WorkspaceCommandResult<WorkspaceDto>.Success(workspace);
    }
}

public sealed record WorkspaceCommandResult<T>(bool Succeeded, T? Value, string? Error)
{
    public static WorkspaceCommandResult<T> Success(T value) => new(true, value, null);

    public static WorkspaceCommandResult<T> Failed(string? error) => new(false, default, error);
}

public sealed record StartedWorkspace(
    WorkspaceDto Workspace,
    IReadOnlyList<ApplicationDefinition> Applications,
    IReadOnlyList<DockerServiceDefinition> Services,
    IReadOnlyList<DotnetApplicationDefinition> Dotnet,
    IReadOnlyList<DockerCapabilityDefinition> Docker);
