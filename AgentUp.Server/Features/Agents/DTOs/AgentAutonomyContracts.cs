using System.ComponentModel.DataAnnotations;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.DTOs;

public sealed record BindAgentRequest([Required, MinLength(1)] string Agent);

public sealed record PermissionRuleDto(
    [Required, MinLength(1)] string Id,
    [Required] PermissionRuleKind Kind,
    bool Allow,
    string? Scope = null);

public sealed record PermissionPolicyRequest
{
    public IReadOnlyList<PermissionRuleDto> Rules { get; init; } = [];
}

public sealed record AgentRoleRequest
{
    [Required, MinLength(1)]
    public string Prompt { get; init; } = "";

    public IReadOnlyList<string> Tools { get; init; } = [];

    [Required]
    public RoleTriggerKind Trigger { get; init; }

    [Required]
    public RoleTerminationKind Termination { get; init; }

    public RoleReviewKind Review { get; init; } = RoleReviewKind.None;
}

public sealed record AutonomyActivationRequest(bool Active);

public sealed record ServerAutonomyRequest(bool Enabled);

public sealed record HostConnectionRequest(bool Authorized);

public sealed record QueueOrderRequest([Required] AssignedIssueOrder Order);

public sealed record AgentLoopStatusDto(
    bool Active,
    string Phase,
    string? TaskId,
    int? TaskNumber,
    string? TaskTitle,
    string? Terminal,
    string? StopReason,
    string? BlockReason,
    IReadOnlyList<string> PermissionRuleIds);

public sealed record AutonomyPreconditionDto(string Id, bool Satisfied, string? Remedy);

public sealed record ServerAutonomyDto(bool Enabled, IReadOnlyList<AutonomyPreconditionDto> Preconditions);

public sealed record AgentBindingResult(AgentSessionDto? Session, bool Found, string? Error, int Status = 409);
