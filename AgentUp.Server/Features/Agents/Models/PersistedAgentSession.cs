using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

public sealed record PersistedAgentSession(
    string WorkspaceId,
    string SessionId,
    AgentKind Agent,
    string Description,
    string Branch,
    DateTimeOffset LastUsedAt);
