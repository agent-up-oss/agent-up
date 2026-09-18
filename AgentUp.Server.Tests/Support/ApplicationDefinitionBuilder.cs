using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds an <see cref="ApplicationDefinition"/> from a sensible default, so a test states
/// only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="ApplicationDefinition"/> positionally.
/// Adding a field to the record changes this one file instead of every test that declares
/// an application.
/// </remarks>
internal sealed class ApplicationDefinitionBuilder(
    string name = ServerDomain.ApiName,
    string command = ServerDomain.ApiCommand)
{
    private string _name = name;
    private string _command = command;
    private string? _path;
    private readonly List<PortDeclaration> _ports = [];
    private Dictionary<string, string>? _environment;
    private List<string>? _environmentFiles;
    private string? _install;
    private bool _database;

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

    public ApplicationDefinitionBuilder WithPort(PortDeclarationBuilder port)
    {
        _ports.Add(port.Build());
        return this;
    }

    public ApplicationDefinitionBuilder WithPort(int defaultPort)
        => WithPort(new PortDeclarationBuilder().Named(null).On(defaultPort));

    public ApplicationDefinitionBuilder WithPort(PortDeclaration port)
    {
        _ports.Add(port);
        return this;
    }

    public ApplicationDefinitionBuilder WithEnvironment(string key, string value)
    {
        _environment ??= [];
        _environment[key] = value;
        return this;
    }

    public ApplicationDefinitionBuilder WithEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        _environment ??= [];
        foreach (var (key, value) in environment)
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

    public ApplicationDefinition Build()
        => new(
            _name,
            _command,
            _path,
            _ports.Count == 0 ? null : _ports,
            _environment,
            _environmentFiles,
            _install,
            _database);
}
