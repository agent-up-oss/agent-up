using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Tests.Support;

/// <summary>
/// Builds an <see cref="ApplicationDefinition"/> from a sensible default, so a test states
/// only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="ApplicationDefinition"/> positionally.
/// </remarks>
internal sealed class ApplicationDefinitionBuilder(
    string name = CliDomain.ApiName,
    string command = CliDomain.ApiCommand)
{
    private string _name = name;
    private string _command = command;
    private string? _path;
    private readonly List<PortDeclaration> _ports = [];
    private Dictionary<string, string>? _environment;
    private List<string>? _environmentFiles;
    private string? _install;
    private bool _database;
    private string? _state;

    public ApplicationDefinitionBuilder Named(string value)
    {
        _name = value;
        return this;
    }

    public ApplicationDefinitionBuilder Running(string value)
    {
        _command = value;
        return this;
    }

    public ApplicationDefinitionBuilder At(string? path)
    {
        _path = path;
        return this;
    }

    public ApplicationDefinitionBuilder WithPort(int defaultPort, string? variable = null)
    {
        _ports.Add(new PortDeclaration(variable, defaultPort));
        return this;
    }

    public ApplicationDefinitionBuilder WithEnvironment(string key, string value)
    {
        _environment ??= [];
        _environment[key] = value;
        return this;
    }

    public ApplicationDefinitionBuilder WithEnvironmentFile(string path)
    {
        _environmentFiles ??= [];
        _environmentFiles.Add(path);
        return this;
    }

    public ApplicationDefinitionBuilder WithInstall(string command)
    {
        _install = command;
        return this;
    }

    public ApplicationDefinitionBuilder AsDatabase()
    {
        _database = true;
        return this;
    }

    public ApplicationDefinitionBuilder InState(string? state)
    {
        _state = state;
        return this;
    }

    public ApplicationDefinition Build()
        => new(
            _name,
            _command,
            _path,
            _ports.Count == 0 ? null : _ports,
            _environment,
            _environmentFiles,
            _install,
            _database)
        {
            State = _state
        };
}
