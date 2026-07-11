using System.Text.Json;
using AgentUp.CLI.Git;
using AgentUp.CLI.Http;
using AgentUp.CLI.Models;

namespace AgentUp.CLI.Commands;

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
        var configPath = Path.Combine(_workingDirectory, "agent-up.json");
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

        var git = new GitReader(_workingDirectory);
        string branch, commit, repoRoot;
        try
        {
            branch = await git.GetBranchAsync();
            commit = await git.GetCommitAsync();
            repoRoot = await git.GetRepoRootAsync();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error: Failed to read git information: {ex.Message}");
            return 1;
        }

        var applications = config.Applications ?? [];

        WorkspaceDto? workspace;
        try
        {
            workspace = await _client.RegisterAsync(new RegisterWorkspaceRequest(
                DisplayName: config.Name,
                RepositoryPath: repoRoot,
                WorktreePath: _workingDirectory,
                Branch: branch,
                Commit: commit)
            {
                Applications = applications
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

        _output.WriteLine($"Workspace \"{workspace.DisplayName}\" definition pushed (id: {workspace.Id})");
        _output.WriteLine($"  Branch:     {workspace.Branch}");
        _output.WriteLine($"  Commit:     {workspace.Commit}");

        if (applications.Count > 0)
        {
            _output.WriteLine($"  Applications ({applications.Count}):");
            foreach (var app in applications)
                _output.WriteLine($"    - {app.Name}: {app.Command}");
        }
        else
        {
            _output.WriteLine("  Applications: none defined");
        }

        return 0;
    }
}
