using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class ApplicationAuditEventViewModel
{
    public ApplicationAuditEventViewModel(ApplicationAuditEventDto dto)
    {
        EventId = dto.EventId;
        Timestamp = FormatTimestamp(dto.Timestamp.ToLocalTime());
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

    private static string FormatTimestamp(DateTimeOffset localTimestamp)
    {
        if (localTimestamp.Date == DateTimeOffset.Now.Date)
            return localTimestamp.ToString("HH:mm:ss");

        return localTimestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
