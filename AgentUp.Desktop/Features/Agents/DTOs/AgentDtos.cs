using System.Text.Json;
using AgentUp.Desktop.Shared.Providers;

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

    public string AutomationId => DesktopAutomationIds.AgentPickerRow(Agent);

    public string EnableAutomationId => DesktopAutomationIds.AgentEnable(Agent);

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
public sealed record AutonomyPreconditionDto(string Id, bool Satisfied, string? Remedy = null)
{
    public bool HasRemedy => !Satisfied && !string.IsNullOrWhiteSpace(Remedy);

    public string Status => Satisfied ? "Ready" : Remedy ?? "Not ready";

    public string AutomationId => DesktopAutomationIds.AgentLoopPrecondition(Id);

    public string Label => Id switch
    {
        "forgeCapabilityEnabled" => "Forge capability",
        "hostConnectionAuthorized" => "Host connection",
        "agentAvailable" => "Agent available",
        "autonomyEnabled" => "Autonomy enabled",
        "workspaceRegistered" => "Workspace registered",
        "boundAgent" => "Bound agent",
        "permissionRequests" => "Permission support",
        "permissionPolicy" => "Permission policy",
        "gitBranch" => "Git branch",
        "unpushedCommits" => "Commits pushed",
        "commitQueueEdit" => "Commit queue",
        "unassignedChanges" => "Working-tree changes",
        _ => Id
    };
}

public sealed record ServerAutonomyDto(bool Enabled, IReadOnlyList<AutonomyPreconditionDto> Preconditions);

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
