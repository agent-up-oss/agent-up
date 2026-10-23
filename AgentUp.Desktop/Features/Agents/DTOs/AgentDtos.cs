using System.Text.Json;

namespace AgentUp.Desktop.Features.Agents.DTOs;

public sealed record AgentDescriptorDto(string Agent, bool Available, string DisplayName);
public sealed record AgentAuthMethodDto(string Id, string Name, string? Description);
public enum AgentLoginTransport
{
    Unknown,
    Poll,
    Code,
    Redirect
}

public sealed record AgentLoginChallengeDto(
    string? Url,
    string? Code,
    string? Instructions,
    AgentLoginTransport Transport = AgentLoginTransport.Unknown,
    bool CanSubmitCode = false,
    DateTimeOffset? ExpiresAt = null,
    string? RedirectUri = null);
public sealed record AgentSessionSummaryDto(string SessionId, string Agent, string Description, string Branch, DateTimeOffset LastUsedAt);
public sealed record AgentLoopStatusDto(
    bool Active,
    string Phase,
    string? TaskId = null,
    int? TaskNumber = null,
    string? TaskTitle = null,
    string? Terminal = null,
    string? StopReason = null,
    string? BlockReason = null,
    IReadOnlyList<string>? PermissionRuleIds = null);
/// <summary>
/// What a workspace's agent is for. The Server stores one role per workspace and sends it with the
/// session; the trigger and termination pair is what distinguishes a builder from a reviewer, so a
/// client reads the role kind from those rather than from any label.
/// </summary>
public sealed record AgentRoleDto(
    string Prompt,
    IReadOnlyList<string> Tools,
    string Trigger,
    string Termination);
public sealed record AgentSessionDto(
    string WorkspaceId,
    string? Agent,
    string State,
    string? SessionId,
    string? Error,
    IReadOnlyList<AgentDescriptorDto> Agents,
    IReadOnlyList<AgentAuthMethodDto>? AuthMethods,
    AgentLoginChallengeDto? LoginChallenge = null,
    IReadOnlyList<AgentSessionSummaryDto>? Sessions = null,
    string? BoundAgent = null,
    AgentLoopStatusDto? Loop = null,
    AgentRoleDto? Role = null);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
