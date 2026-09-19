using AgentUp.Desktop.Features.Ports.DTOs;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// Builds a <see cref="PortMappingDto"/> from the canonical allocated port in
/// <see cref="DesktopDomain"/>, so a test states only the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="PortMappingDto"/> positionally. Its two
/// adjacent int fields are exactly the pair a reader cannot tell apart at a call site.
/// </remarks>
internal sealed class PortMappingDtoBuilder
{
    private string? _variable;
    private int _defaultPort = DesktopDomain.HttpPort;
    private int? _allocatedPort;
    private string _protocol = "http";

    public PortMappingDtoBuilder Named(string? variable)
    {
        _variable = variable;
        return this;
    }

    /// <summary>Declares and allocates the same port, the common case.</summary>
    public PortMappingDtoBuilder On(int port)
    {
        _defaultPort = port;
        _allocatedPort = port;
        return this;
    }

    public PortMappingDtoBuilder Declaring(int defaultPort)
    {
        _defaultPort = defaultPort;
        return this;
    }

    public PortMappingDtoBuilder AllocatedTo(int allocatedPort)
    {
        _allocatedPort = allocatedPort;
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
