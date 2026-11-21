namespace AgentUp.Server.Features.Agents.Models;

public static class AgentUnavailability
{
    public const string CapabilityNotEnabled = "capabilityNotEnabled";
    public const string ExecutableNotFound = "executableNotFound";

    /// <summary>The software the agent's capability runs is missing on the Server host, so it cannot be enabled.</summary>
    public const string SoftwareNotFound = "softwareNotFound";

    /// <summary>The capability is enabled but a required setting, such as OpenCode's endpoint, has no value.</summary>
    public const string SettingsRequired = "settingsRequired";

    public const string ScriptedTestAgent = "test-agent-scripted";
    public const string ScriptedTestAgentDisplayName = "Scripted test agent";

    public static string CapabilityNotEnabledDetail(string displayName)
        => $"{displayName} is not enabled on this Server.";

    public static string CapabilityNotEnabledRemedy(string agentId)
        => $"Enable the {agentId} capability.";

    public static string ExecutableNotFoundDetail(string agentId)
        => $"{agentId} ACP executable is not installed or is not on PATH.";

    public static string ExecutableNotFoundRemedy(string? command, string agentId)
        => string.IsNullOrWhiteSpace(command)
            ? $"Install the {agentId} ACP executable and keep it on PATH."
            : $"Install {command} and keep it on PATH.";

    public static string SoftwareNotFoundDetail(string displayName, IReadOnlyList<string> missing)
        => $"{displayName} needs {string.Join(", ", missing)}, which was not found on the Server host.";

    public static string SoftwareNotFoundRemedy(IReadOnlyList<string> missing)
        => missing.Contains("nix")
            ? "Install Nix: https://nixos.org/download"
            : $"Install {string.Join(", ", missing)} and keep it on PATH.";

    public static string SettingsRequiredDetail(string displayName, IReadOnlyList<string> labels)
        => $"{displayName} needs {string.Join(" and ", labels)} before it can start.";

    public static string SettingsRequiredRemedy(string displayName)
        => $"Set them under {displayName} in Settings.";
}
