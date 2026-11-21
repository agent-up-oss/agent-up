namespace AgentUp.Server.Features.Agents.Models;

/// <param name="Environment">Variables the capability adds for this agent, such as OpenCode's endpoint configuration.</param>
public sealed record AgentCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string>? Environment = null);
