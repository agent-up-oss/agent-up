using System.Text.Json;
using AgentUp.Server.Features.Mcp.DTOs;
using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Providers;

public sealed class AgentUpConfigurationProvider : IAgentUpConfigurationProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var configPath = Path.Join(worktreePath, "agent-up.json");
        if (!File.Exists(configPath))
            return null;

        await using var stream = File.OpenRead(configPath);
        return await JsonSerializer.DeserializeAsync<AgentUpConfiguration>(
            stream,
            JsonOptions,
            cancellationToken);
    }
}
