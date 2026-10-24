namespace AgentUp.Server.Features.Agents.Models;

public static class AgentUnavailability
{
    public const string CapabilityNotEnabled = "capabilityNotEnabled";
    public const string ExecutableNotFound = "executableNotFound";
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
}
