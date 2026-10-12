using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Hard loop invariants. Any violation fails the run regardless of outcome score.
/// </summary>
public sealed class LoopInvariantGuard
{
    public const int MaxTranscriptEvents = 256;

    public IReadOnlyList<string> Evaluate(LoopRunResult result)
    {
        var violations = new List<string>();
        if (!Enum.IsDefined(result.Terminal))
            violations.Add("The run ended in an untyped terminal state.");
        if (string.IsNullOrWhiteSpace(result.StopReason) && result.Terminal is not LoopTerminal.BlockedPolicy)
            violations.Add("The run ended without a typed stop reason.");
        if (!result.PermissionsDrained)
            violations.Add("A permission request was left on the human-facing queue.");
        if (!result.PromptGateReleased)
            violations.Add("The prompt gate was still held after the run.");
        if (result.TranscriptEvents > MaxTranscriptEvents)
            violations.Add("Transcript size exceeded the bound.");
        if (result.ContextChars < 0)
            violations.Add("Context accounting went negative.");
        if (result.Terminal == LoopTerminal.Completed && !result.CompletedTask)
            violations.Add("A completed terminal did not record a completed task.");
        if (result.Terminal == LoopTerminal.BlockedPolicy && string.IsNullOrWhiteSpace(result.BlockReason))
            violations.Add("A policy block was not typed.");
        if (result.PermissionRuleIds.Any(string.IsNullOrWhiteSpace))
            violations.Add("A permission was resolved without a recorded rule id.");
        return violations;
    }
}
