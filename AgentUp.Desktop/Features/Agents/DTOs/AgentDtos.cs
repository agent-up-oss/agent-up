using System.Text.Json;

namespace AgentUp.Desktop.Features.Agents.DTOs;

public sealed record AgentDescriptorDto(string Agent, bool Available, string DisplayName);
public sealed record AgentAuthMethodDto(string Id, string Name, string? Description);
public sealed record AgentLoginChallengeDto(string? Url, string? Code, string? Instructions);
public sealed record AgentSessionSummaryDto(string SessionId, string Agent, string Description, string Branch, DateTimeOffset LastUsedAt)
{
    public bool IsCurrent { get; set; }
}
public sealed record AgentSessionDto(
    string WorkspaceId,
    string? Agent,
    string State,
    string? SessionId,
    string? Error,
    IReadOnlyList<AgentDescriptorDto> Agents,
    IReadOnlyList<AgentAuthMethodDto>? AuthMethods,
    AgentLoginChallengeDto? LoginChallenge = null,
    IReadOnlyList<AgentSessionSummaryDto>? Sessions = null);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
