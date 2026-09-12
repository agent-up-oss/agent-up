using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// Builds a <see cref="CheckDefinition"/> from a sensible default, so a test states only
/// the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="CheckDefinition"/> positionally. Adding a
/// field to the record changes this one file instead of every test that mentions a check.
/// </remarks>
internal sealed class CheckBuilder(string id)
{
    private string _command = "true";
    private string? _workingDirectory;
    private CheckTier _tier = CheckTier.Fast;
    private IReadOnlyList<string> _platforms = [];
    private bool _ciOnly;
    private int _order;
    private IReadOnlyList<string> _inputs = [];

    public string Id => id;

    public CheckBuilder WithCommand(string command)
    {
        _command = command;
        return this;
    }

    public CheckBuilder WithWorkingDirectory(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        return this;
    }

    public CheckBuilder WithTier(CheckTier tier)
    {
        _tier = tier;
        return this;
    }

    public CheckBuilder WithPlatforms(params string[] platforms)
    {
        _platforms = platforms;
        return this;
    }

    public CheckBuilder WithCiOnly()
    {
        _ciOnly = true;
        return this;
    }

    public CheckBuilder WithOrder(int order)
    {
        _order = order;
        return this;
    }

    public CheckBuilder WithInputs(params string[] inputs)
    {
        _inputs = inputs;
        return this;
    }

    public CheckDefinition Build()
        => new(id, _command, _workingDirectory, _tier, _platforms, _ciOnly, _order, _inputs);
}
