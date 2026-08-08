using System.Text.Json.Serialization;

namespace AgentUp.Desktop.Features.Workspaces.DTOs;

internal sealed record WorkspaceStateChangedEventDto(
    string WorkspaceId,
    string State,
    [property: JsonPropertyName("applications")] IReadOnlyList<AppStateChangeDto> Applications,
    string? HealthState = null);

internal sealed record AppStateChangeDto(
    string Name,
    string State,
    IReadOnlyList<PortHealthChangeDto>? PortHealth = null);

internal sealed record PortHealthChangeDto(int AllocatedPort, string HealthState);
