using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Models;

namespace AgentUp.CLI.Features.Workspaces.Providers;

public sealed class WorkspaceConfigurationProvider : IWorkspaceConfigurationProvider
{
    public async Task<WorkspaceConfigurationResult> LoadAsync(string workingDirectory)
    {
        var configPath = Path.Join(workingDirectory, "agent-up.json");
        if (!File.Exists(configPath))
            return new WorkspaceConfigurationResult(null, "Error: agent-up.json not found in current directory.");

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<AgentUpJson>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("agent-up.json is empty or null.");
            return new WorkspaceConfigurationResult(config, null);
        }
        catch (Exception ex)
        {
            return new WorkspaceConfigurationResult(null, $"Error: Failed to read agent-up.json: {ex.Message}");
        }
    }
}
