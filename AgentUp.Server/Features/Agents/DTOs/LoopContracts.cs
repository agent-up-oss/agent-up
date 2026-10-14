using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.DTOs;

/// <summary>How an autonomous loop run stopped. Every run ends in exactly one of these.</summary>
public enum LoopTerminal
{
    Completed,
    EndTurn,
    Cancelled,
    MaxIterations,
    BlockedPolicy,
    BlockedAwaitingInput,
    BlockedRateLimited,
    BlockedPrecondition,
    BlockedHostUnavailable,
    Failed,
    Idle
}

public sealed record LoopRunRequest(
    string WorkspaceId,
    string Task,
    int MaxIterations,
    IAgentPermissionPolicy Policy,
    int ContextCharLimit,
    uint Seed = 42,
    int StandingPolicyReplies = 2,
    int PromptTimeoutMs = 30_000);

public sealed record LoopRunResult(
    LoopTerminal Terminal,
    string? StopReason,
    int Iterations,
    IReadOnlyList<string> PermissionRuleIds,
    bool PermissionsDrained,
    bool PromptGateReleased,
    int TranscriptEvents,
    int ContextChars,
    bool CompletedTask,
    string? BlockReason,
    uint Seed);
