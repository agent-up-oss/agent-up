using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class ApplicationAuditEventViewModel
{
    public ApplicationAuditEventViewModel(ApplicationAuditEventDto dto)
    {
        EventId = dto.EventId;
        Timestamp = dto.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        var presentation = DiagnosticEventPresentation.Present(dto);
        Category = presentation.Category;
        CategoryColor = presentation.CategoryColor;
        Message = presentation.Message;
        MessageColor = presentation.MessageColor;
    }

    public string EventId { get; }
    public string Timestamp { get; }
    public string Category { get; }
    public string CategoryColor { get; }
    public string Message { get; }
    public string MessageColor { get; }
}
