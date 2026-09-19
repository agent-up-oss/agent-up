using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Tests.Support;

/// <summary>
/// Builds a <see cref="DebugCommandDto"/> from a sensible default, so a test states only
/// the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="DebugCommandDto"/> positionally. Four
/// consecutive nullable strings and a timeout are exactly what a reader cannot decode at a
/// call site.
/// </remarks>
internal sealed class DebugCommandDtoBuilder(string verb, string? surface = null)
{
    private string? _action;
    private string? _workspaceName;
    private string? _password;
    private TimeSpan _timeout = DebugDomain.Timeout;
    private bool _detach;
    private string? _suite;

    public DebugCommandDtoBuilder Doing(string? action)
    {
        _action = action;
        return this;
    }

    public DebugCommandDtoBuilder ForWorkspace(string? workspaceName)
    {
        _workspaceName = workspaceName;
        return this;
    }

    public DebugCommandDtoBuilder WithPassword(string? password)
    {
        _password = password;
        return this;
    }

    public DebugCommandDtoBuilder TimingOutAfter(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public DebugCommandDtoBuilder Detached()
    {
        _detach = true;
        return this;
    }

    public DebugCommandDtoBuilder WithSuite(string? suite)
    {
        _suite = suite;
        return this;
    }

    public DebugCommandDto Build()
        => new(verb, surface, _action, _workspaceName, _password, _timeout, _detach, _suite);
}
