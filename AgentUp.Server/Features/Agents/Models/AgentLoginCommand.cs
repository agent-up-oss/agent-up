namespace AgentUp.Server.Features.Agents.Models;

public sealed record AgentLoginCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment);
