namespace AgentUp.Server.Features.Agents.Models;

public sealed record AgentCommand(string FileName, IReadOnlyList<string> Arguments);
