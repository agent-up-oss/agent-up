using AgentUp.Desktop.Features.Ports.DTOs;

namespace AgentUp.Tests.Support;

/// <summary>
/// Builds a <see cref="PortMappingDto"/> for the end-to-end suite, so its two adjacent int
/// fields are named at the call site rather than read by position.
/// </summary>
internal sealed class PortMappingDtoBuilder
{
    private string? _variable = ProductDomain.PortVariable;
    private int _defaultPort;
    private int? _allocatedPort;
    private string _protocol = "http";

    public PortMappingDtoBuilder Named(string? variable)
    {
        _variable = variable;
        return this;
    }

    /// <summary>Declares and allocates the same port, as a running application reports it.</summary>
    public PortMappingDtoBuilder On(int port)
    {
        _defaultPort = port;
        _allocatedPort = port;
        return this;
    }

    public PortMappingDtoBuilder WithProtocol(string protocol)
    {
        _protocol = protocol;
        return this;
    }

    public PortMappingDto Build()
        => new(_variable, _defaultPort, _allocatedPort ?? _defaultPort, _protocol);
}
