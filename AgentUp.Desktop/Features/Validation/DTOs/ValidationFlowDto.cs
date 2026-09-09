namespace AgentUp.Desktop.Features.Validation.DTOs;
public sealed record ValidationStepDto(string Id, string Description);
public sealed record ValidationFlowDto(string Id, string WorkspaceId, string Application, string Name, string Description, string InitialPath, IReadOnlyList<ValidationStepDto> Steps, int Version);
