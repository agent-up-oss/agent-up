using System.Text.Json.Serialization;

namespace AgentUp.Server.Features.Ports.DTOs;

public record PortDeclaration(
    string? Variable,
    int DefaultPort,
    string Protocol = "http",
    [property: JsonPropertyName("healthCheck")] string? HealthCheckPath = null,
    [property: JsonPropertyName("metrics")] string? MetricsPath = null);
