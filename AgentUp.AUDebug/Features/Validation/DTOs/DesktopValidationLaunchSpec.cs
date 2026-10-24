namespace AgentUp.AUDebug.Features.Validation.DTOs;

public sealed record DesktopValidationLaunchSpec(
    string OutputPath,
    string? FlowId,
    string? WorkspaceId,
    string? Password);
