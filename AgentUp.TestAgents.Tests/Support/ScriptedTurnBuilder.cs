using AgentUp.TestAgents.Features.Scripted.Models;

namespace AgentUp.TestAgents.Tests.Support;

internal sealed class ScriptedTurnBuilder(ScriptedTurnKind kind)
{
    private string? _toolKind;
    private string? _path;
    private string? _name;
    private string? _error;

    public ScriptedTurnBuilder WithToolKind(string toolKind)
    {
        _toolKind = toolKind;
        return this;
    }

    public ScriptedTurnBuilder WithPath(string path)
    {
        _path = path;
        return this;
    }

    public ScriptedTurnBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ScriptedTurnBuilder WithError(string error)
    {
        _error = error;
        return this;
    }

    public ScriptedTurn Build() => new(kind, _toolKind, Path: _path, Name: _name, Error: _error);
}
