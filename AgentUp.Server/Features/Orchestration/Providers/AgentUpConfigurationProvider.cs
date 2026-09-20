using System.Text.Json;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Interfaces;

namespace AgentUp.Server.Features.Orchestration.Providers;

public sealed class AgentUpConfigurationProvider(IEnabledCapabilityPackages? packages = null)
    : IAgentUpConfigurationProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var configPath = Path.Join(worktreePath, "agent-up.json");
        if (!File.Exists(configPath))
            return null;

        await using var stream = File.OpenRead(configPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return AgentUpConfigurationParser.Parse(document.RootElement, packages?.ListRuntimes() ?? [], JsonOptions);
    }
}
