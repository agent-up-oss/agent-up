using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Models;
using AgentUp.CLI.Shared.Providers;

namespace AgentUp.CLI.Features.Workspaces.Providers;

public sealed class WorkspaceConfigurationProvider : IWorkspaceConfigurationProvider
{
    public async Task<WorkspaceConfigurationResult> LoadAsync(string workingDirectory)
    {
        var workspaceRoot = WorkspaceRootProvider.Find(workingDirectory);
        if (workspaceRoot is null)
            return new WorkspaceConfigurationResult(null, null, "Error: agent-up.json not found in the current directory or any parent directory.");

        var configPath = Path.Join(workspaceRoot, "agent-up.json");

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<AgentUpJson>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("agent-up.json is empty or null.");
            return new WorkspaceConfigurationResult(config, workspaceRoot, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new WorkspaceConfigurationResult(null, workspaceRoot, $"Error: Failed to read agent-up.json: {ex.Message}");
        }
    }
}
