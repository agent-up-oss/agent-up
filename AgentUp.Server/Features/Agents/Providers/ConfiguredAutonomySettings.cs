using AgentUp.Server.Features.Agents.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class ConfiguredAutonomySettings(
    IConfiguration configuration,
    IAgentWorkspaceSettingsRepository settings) : IAutonomySettings
{
    private bool _persisted;

    public bool ServerEnabled =>
        IsTrue(configuration["AGENTUP_AUTONOMY_ENABLED"]) || _persisted;

    public bool TryEnable(out string? refusal)
    {
        if (!settings.List().Any(item => item.HostConnectionAuthorized))
        {
            refusal = "A repository host connection must be authorized before autonomy can be enabled on this Server.";
            return false;
        }

        _persisted = true;
        refusal = null;
        return true;
    }

    public void Disable() => _persisted = false;

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.Ordinal);
}
