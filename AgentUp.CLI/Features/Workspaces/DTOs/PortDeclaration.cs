using System.Text.Json.Serialization;

namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record PortDeclaration(
    string? Variable,
    int DefaultPort,
    string Protocol = "http",
    [property: JsonPropertyName("healthCheck")] string? HealthCheckPath = null,
    [property: JsonPropertyName("metrics")] string? MetricsPath = null);
