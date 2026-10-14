namespace AgentUp.Server.Features.Agents.DTOs;

/// <summary>What the loop kernel needs from the live session after a prompt.</summary>
public sealed record LoopInspection(
    IReadOnlyList<string> PermissionRuleIds,
    bool PermissionsDrained,
    bool PromptGateReleased,
    int TranscriptEvents,
    bool CompletedTask,
    string? StopReason,
    string? BlockReason,
    bool SawToolCall = false);
