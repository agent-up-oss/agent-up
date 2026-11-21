namespace AgentUp.Server.Features.Agents.Models;

/// <summary>The prompt for one turn on a task, or why the clone could not be made ready for it.</summary>
public sealed record TaskTurnPlan(string? Prompt, string? Refusal)
{
    public static TaskTurnPlan Ready(string prompt) => new(prompt, null);

    public static TaskTurnPlan Refused(string refusal) => new(null, refusal);
}

/// <summary>
/// What became of a finished turn: published, or blocked with a stop reason and the cause. A block
/// keeps the task with its member, so the same member picks it back up.
/// </summary>
public sealed record TaskTurnOutcome(bool Published, string? StopReason, string? Cause)
{
    public static TaskTurnOutcome Done() => new(true, null, null);

    public static TaskTurnOutcome Blocked(string stopReason, string cause) => new(false, stopReason, cause);
}
