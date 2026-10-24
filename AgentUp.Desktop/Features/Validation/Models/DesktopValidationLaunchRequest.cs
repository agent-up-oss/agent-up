namespace AgentUp.Desktop.Features.Validation.Models;

public sealed record DesktopValidationLaunchRequest(
    string OutputRoot,
    string? FlowId,
    string? WorkspaceId,
    bool SkipCosmeticStaging);
