namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentRuntimePathProvider
{
    IReadOnlyList<string> PathPrefixes();

    string? RuntimeRoot();
}
