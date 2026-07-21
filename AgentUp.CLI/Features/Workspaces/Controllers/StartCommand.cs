using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StartCommand
{
    private readonly WorkspaceApiClient _client;
    private readonly IWorkspaceConfigurationProvider _configuration;
    private readonly IWorkspaceIdentityProvider _identity;
    private readonly string _workingDirectory;
    private readonly TextWriter _output;

    public StartCommand(
        WorkspaceApiClient client,
        IWorkspaceConfigurationProvider configuration,
        IWorkspaceIdentityProvider identity,
        string workingDirectory,
        TextWriter output)
    {
        _client = client;
        _configuration = configuration;
        _identity = identity;
        _workingDirectory = workingDirectory;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var loaded = await _configuration.LoadAsync(_workingDirectory);
        if (!loaded.Succeeded)
        {
            _output.WriteLine(loaded.Error);
            return 1;
        }

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
            _output.WriteLine($"Error: Failed to push workspace definition: {ex.Message}");
            return 1;
        }

        if (workspace is null)
        {
            _output.WriteLine("Error: Server returned an unexpected response.");
            return 1;
        }

        try
        {
            await _client.StartWorkspaceAsync(workspace.Id);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _output.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        _output.WriteLine($"Started workspace \"{workspace.DisplayName}\"");
        _output.WriteLine($"  Branch:  {workspace.Branch}");
        _output.WriteLine($"  Commit:  {workspace.Commit}");

        if (applications.Count > 0)
        {
            _output.WriteLine($"  Applications ({applications.Count}):");
            foreach (var app in applications)
                _output.WriteLine($"    - {app.Name}: {app.Command}");
        }

        if (services.Count > 0)
        {
            _output.WriteLine($"  Services ({services.Count}):");
            foreach (var svc in services)
                _output.WriteLine($"    - {svc.Name}: {svc.Image}");
        }

        if (dotnet.Count > 0)
        {
            _output.WriteLine($"  .NET ({dotnet.Count}):");
            foreach (var app in dotnet)
                _output.WriteLine($"    - {app.Name}: dotnet run --project {app.Run.Project}");
        }

        if (docker.Count > 0)
        {
            _output.WriteLine($"  Docker ({docker.Count}):");
            foreach (var svc in docker)
                _output.WriteLine($"    - {svc.Name}: {svc.Image}");
        }

        return 0;
    }
}
