using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace AgentUp.Server.Features.Agents.DTOs;

public enum AgentKind { Codex, Cursor, Claude }

public sealed record ScheduleAgentRequest(AgentKind Agent);
public sealed record AgentPromptRequest([Required, MaxLength(100_000)] string Message);
public sealed record AgentPermissionResponse(
    [Required, MinLength(1)] string RequestId,
    [Required, MinLength(1)] string OptionId);
public sealed record AgentAuthenticationRequest([Required, MinLength(1)] string MethodId);
public sealed record AgentAuthMethodDto(string Id, string Name, string? Description);
public sealed record AgentDescriptor(AgentKind Agent, bool Available, string DisplayName);
public sealed record AgentSessionDto(
    string WorkspaceId,
    AgentKind? Agent,
    string State,
    string? SessionId,
    string? Error,
    IReadOnlyList<AgentDescriptor> Agents,
    IReadOnlyList<AgentAuthMethodDto> AuthMethods);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
public sealed record AgentScheduleResult(AgentSessionDto? Session, bool Found, string? Error);
public sealed record AgentActionResult(bool Found, string? Error)
{
    public static AgentActionResult Success() => new(true, null);
    public static AgentActionResult NotFound() => new(false, null);
    public static AgentActionResult Failed(string error) => new(true, error);
}
