namespace AgentUp.Server.Features.Agents.Models;

public static class AgentUnavailability
{
    public const string CapabilityNotEnabled = "capabilityNotEnabled";
    public const string ExecutableNotFound = "executableNotFound";
    public const string ScriptedTestAgent = "test-agent-scripted";
    public const string ScriptedTestAgentDisplayName = "Scripted test agent";
    public const string LocalInferenceTestAgent = "test-agent-infer";
    public const string LocalInferenceTestAgentDisplayName = "Local inference test agent";

    public static readonly string[] DevelopmentAgents =
    [
        ScriptedTestAgent,
        LocalInferenceTestAgent
    ];

    public static bool IsDevelopmentAgent(string agent) =>
        DevelopmentAgentId(agent) is not null;

    public static string? DevelopmentAgentId(string agent) =>
        DevelopmentAgents.FirstOrDefault(id => id.Equals(agent, StringComparison.OrdinalIgnoreCase));

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
