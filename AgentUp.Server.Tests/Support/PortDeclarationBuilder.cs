using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds a <see cref="PortDeclaration"/> from a sensible default, so a test states only
/// the attribute it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="PortDeclaration"/> positionally. Adding a
/// field to the record changes this one file instead of every test that declares a port.
/// </remarks>
internal sealed class PortDeclarationBuilder
{
    private string? _variable = ServerDomain.PortVariable;
    private int _defaultPort = ServerDomain.DefaultPort;
    private string _protocol = "http";
    private string? _healthCheckPath;
    private string? _metricsPath;

    public PortDeclarationBuilder Named(string? variable)
    {
        _variable = variable;
        return this;
    }

    public PortDeclarationBuilder On(int defaultPort)
    {
        _defaultPort = defaultPort;
        return this;
    }

    public PortDeclarationBuilder WithProtocol(string protocol)
    {
        _protocol = protocol;
        return this;
    }

    public PortDeclarationBuilder WithHealthCheck(string path)
    {
        _healthCheckPath = path;
        return this;
    }

    public PortDeclarationBuilder WithMetrics(string path)
    {
        _metricsPath = path;
        return this;
    }

    public PortDeclaration Build()
        => new(_variable, _defaultPort, _protocol, _healthCheckPath, _metricsPath);
}
