namespace AgentUp.TestAgents.Features.Scripted.Models;

public sealed record ScriptedAgentScript(IReadOnlyList<ScriptedTurn> Turns)
{
    public static ScriptedAgentScript Empty { get; } = new([]);
}
