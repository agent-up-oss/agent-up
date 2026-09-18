using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds an <see cref="ApplicationDto"/> from the canonical application in
/// <see cref="DesktopDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="ApplicationDto"/> positionally, and so
/// "the same application but stopped" is a call rather than another shared fixture method.
/// </remarks>
internal sealed class ApplicationDtoBuilder(
    string name = DesktopDomain.ApiName,
    string command = DesktopDomain.ApiCommand)
{
    private string _name = name;
    private string _command = command;
    private string? _path;
    private string _state = DesktopDomain.RunningState;
    private string _kind = DesktopDomain.ProcessKind;
    private List<PortMappingDto> _ports = [];
    private bool _database;

    public ApplicationDtoBuilder Named(string value)
    {
        _name = value;
        return this;
    }

    public ApplicationDtoBuilder Running(string value)
    {
        _command = value;
        return this;
    }

    public ApplicationDtoBuilder At(string? path)
    {
        _path = path;
        return this;
    }

    public ApplicationDtoBuilder InState(string state)
    {
        _state = state;
        return this;
    }

    public ApplicationDtoBuilder Stopped() => InState(DesktopDomain.StoppedState);

    public ApplicationDtoBuilder OfKind(string kind)
    {
        _kind = kind;
        return this;
    }

    public ApplicationDtoBuilder WithPort(PortMappingDtoBuilder port)
    {
        _ports.Add(port.Build());
        return this;
    }

    public ApplicationDtoBuilder WithPort(PortMappingDto port)
    {
        _ports.Add(port);
        return this;
    }

    public ApplicationDtoBuilder WithPort(int port)
        => WithPort(new PortMappingDtoBuilder().On(port));

    /// <summary>
    /// Sets the whole allocated-port list, including to null, so the tests that cover a
    /// payload with no "allocatedPorts" member can still say so.
    /// </summary>
    public ApplicationDtoBuilder WithPorts(List<PortMappingDto> ports)
    {
        _ports = ports;
        return this;
    }

    public ApplicationDtoBuilder AsDatabase()
    {
        _database = true;
        return this;
    }

    public ApplicationDto Build()
        => new(_name, _command, _path, _state, _kind)
        {
            AllocatedPorts = _ports,
            Database = _database
        };
}
