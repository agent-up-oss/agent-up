namespace AgentUp.TestAgents.Features.Scripted.Models;

/// <summary>The declared turns, played in order, or the plays a prompt selects when there are any.</summary>
public sealed record ScriptedAgentScript(IReadOnlyList<ScriptedTurn> Turns, IReadOnlyList<ScriptedPlay>? Plays = null)
{
    public static ScriptedAgentScript Empty { get; } = new([]);
}
