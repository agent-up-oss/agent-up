namespace AgentUp.Desktop.Features.Audit.ViewModels;

internal sealed record DiagnosticPresentation(
    string Category,
    string CategoryColor,
    string Message,
    string MessageColor);
