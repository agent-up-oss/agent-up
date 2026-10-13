using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

public sealed record LoopStatusSnapshot(
    bool Active,
    string Phase,
    AssignedIssue? CurrentTask,
    LoopTerminal? Terminal,
    string? StopReason,
    string? BlockReason,
    IReadOnlyList<string> PermissionRuleIds);
