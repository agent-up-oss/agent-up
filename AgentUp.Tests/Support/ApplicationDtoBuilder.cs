using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;

namespace AgentUp.Tests.Support;

/// <summary>
/// Builds an <see cref="ApplicationDto"/> for the end-to-end suite, so a test states only
/// the attribute it is actually about.
/// </summary>
internal sealed class ApplicationDtoBuilder(string name, string command)
{
    private string? _path;
    private string _state = ProductDomain.RunningState;
    private string _kind = "Process";
    private readonly List<PortMappingDto> _ports = [];

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

    public ApplicationDto Build()
        => new(name, command, _path, _state, _kind) { AllocatedPorts = _ports };
}
