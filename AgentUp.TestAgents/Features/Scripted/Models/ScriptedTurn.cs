namespace AgentUp.TestAgents.Features.Scripted.Models;

public sealed record ScriptedTurn(
    ScriptedTurnKind Kind,
    string? ToolKind = null,
    string? Title = null,
    string? Path = null,
    string? Command = null,
    string? Name = null,
    string? Error = null);
