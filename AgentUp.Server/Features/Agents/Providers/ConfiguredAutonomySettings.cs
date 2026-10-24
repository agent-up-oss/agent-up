using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class ConfiguredAutonomySettings(
    IConfiguration configuration,
    IAgentWorkspaceSettingsRepository settings,
    IHostConnectionPresence? host = null) : IAutonomySettings
{
    public bool ServerEnabled => EnvironmentForced || settings.ServerEnabled;

    public bool TryEnable(out string? refusal)
    {
        if (!HostAuthorized())
        {
            refusal = AutonomyPreconditions.Refusal(AutonomyPreconditions.HostConnectionAuthorized);
            return false;
        }

        settings.SetServerEnabled(true);
        refusal = null;
        return true;
    }

    public void Disable() => settings.SetServerEnabled(false);

    private bool EnvironmentForced => IsTrue(configuration["AGENTUP_AUTONOMY_ENABLED"]);

    private bool HostAuthorized() =>
        host?.IsPresent() ?? settings.List().Any(item => item.HostConnectionAuthorized);

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.Ordinal);
}
