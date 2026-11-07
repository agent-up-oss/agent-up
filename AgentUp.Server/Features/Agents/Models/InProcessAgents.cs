namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Agents the Server runs itself rather than launching an ACP coding CLI. They are listed only
/// when configured and carry no capability package: there is nothing to deliver through Nix.
/// </summary>
public static class InProcessAgents
{
    public const string Ollama = "ollama";
    public const string OllamaDisplayName = "Ollama (in-process)";
    public const string OllamaModelSetting = "AGENTUP_OLLAMA_MODEL";
    public const string OllamaUrlSetting = "AGENTUP_OLLAMA_URL";
    public const string OllamaDefaultUrl = "http://127.0.0.1:11434";

    public static bool IsInProcess(string agent) =>
        agent.Equals(Ollama, StringComparison.OrdinalIgnoreCase);

    public static AgentFixtureKind FixtureOf(string agent) =>
        IsInProcess(agent) ? AgentFixtureKind.InProcess : AgentFixtureKind.CodingAgent;
}
