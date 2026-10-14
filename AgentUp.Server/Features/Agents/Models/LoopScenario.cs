namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// A reusable loop-coverage prompt. Scenarios do not name an agent, so the same
/// files drive CPU inference on every change and a real CLI on demand.
/// </summary>
public sealed record LoopScenario(
    string Id,
    string Prompt,
    bool ScoresOutcome,
    string? ExpectTool,
    bool ExpectCompleted);
