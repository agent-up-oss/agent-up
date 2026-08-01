using System.Text.Json.Serialization;

namespace AgentUp.Desktop.Features.Workspaces.DTOs;

internal sealed record WorkspaceStateChangedEventDto(
    string WorkspaceId,
    string State,
    [property: JsonPropertyName("applications")] IReadOnlyList<AppStateChangeDto> Applications);

internal sealed record AppStateChangeDto(string Name, string State);
