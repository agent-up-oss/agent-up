using System.Text.Json;

namespace AgentUp.Server.Features.Agents.DTOs;

public enum AgentKind { Codex, Cursor, Claude }

public sealed record ScheduleAgentRequest(AgentKind Agent);
public sealed record AgentPromptRequest(string Message);
public sealed record AgentPermissionResponse(string RequestId, string OptionId);
public sealed record AgentDescriptor(AgentKind Agent, bool Available, string DisplayName);
public sealed record AgentSessionDto(
    string WorkspaceId,
    AgentKind? Agent,
    string State,
    string? SessionId,
    string? Error,
    IReadOnlyList<AgentDescriptor> Agents);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
public sealed record AgentScheduleResult(AgentSessionDto? Session, bool Found, string? Error);
