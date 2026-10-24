namespace AgentUp.Desktop.Features.Validation.DTOs;

public sealed record DesktopResolvedTargetDto(
    string ControlType,
    string? AutomationId,
    double X,
    double Y,
    double Width,
    double Height);
