using System.Text.Json;

namespace AgentUp.Desktop.Features.Agents.DTOs;

public sealed record AgentDescriptorDto(
    string Agent,
    bool Available,
    string DisplayName,
    string? Unavailability = null,
    string? Detail = null,
    string? Remedy = null)
{
    public const string CapabilityNotEnabled = "capabilityNotEnabled";
    public const string ExecutableNotFound = "executableNotFound";

    public bool CanEnable =>
        !Available && string.Equals(Unavailability, CapabilityNotEnabled, StringComparison.Ordinal);

    public bool IsBlocked => !Available && !CanEnable;

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool HasRemedy => !string.IsNullOrWhiteSpace(Remedy);

    public string StatusLabel => Available
        ? "Available"
        : CanEnable
            ? "Not enabled"
            : "Executable not found";
}
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
    AgentLoopStatusDto? Loop = null);
public sealed record AgentEventDto(long Sequence, string Type, JsonElement Payload, DateTimeOffset Timestamp);
