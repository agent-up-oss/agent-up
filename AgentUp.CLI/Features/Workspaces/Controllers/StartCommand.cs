using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Features.Workspaces.Controllers;

public sealed class StartCommand
{
    private readonly WorkspaceApiClient _client;
    private readonly string _workingDirectory;
    private readonly TextWriter _output;

    public StartCommand(WorkspaceApiClient client, string workingDirectory, TextWriter output)
    {
        _client = client;
        _workingDirectory = workingDirectory;
        _output = output;
    }

    public async Task<int> RunAsync()
    {
        var configPath = Path.Join(_workingDirectory, "agent-up.json");
        if (!File.Exists(configPath))
        {
            _output.WriteLine("Error: agent-up.json not found in current directory.");
            return 1;
        }

        AgentUpJson config;
        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            config = JsonSerializer.Deserialize<AgentUpJson>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("agent-up.json is empty or null.");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: Failed to read agent-up.json: {ex.Message}");
            return 1;
        }

        var git = await ReadGitMetadataAsync(_workingDirectory);

        var applications = config.Applications ?? [];
        var services = config.Services ?? [];

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
                Services = services
            });
        }
        catch (Exception ex)
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
        catch (Exception ex)
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

        return 0;
    }

    private static async Task<GitMetadata> ReadGitMetadataAsync(string workingDirectory)
    {
        var git = new GitReader(workingDirectory);
        try
        {
            return new GitMetadata(
                RepositoryPath: await git.GetRepoRootAsync(),
                Branch: await git.GetBranchAsync(),
                Commit: await git.GetCommitAsync());
        }
        catch
        {
            return new GitMetadata(
                RepositoryPath: workingDirectory,
                Branch: "not on a git branch",
                Commit: "");
        }
    }

    private sealed record GitMetadata(string RepositoryPath, string Branch, string Commit);
}
