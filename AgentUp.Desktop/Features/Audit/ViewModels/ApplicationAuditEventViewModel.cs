using AgentUp.Desktop.Features.Audit.DTOs;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class ApplicationAuditEventViewModel(ApplicationAuditEventDto dto)
{
    public string Timestamp => dto.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string Action => dto.Action;
    public string Outcome => dto.Outcome;
    public string Details => string.Join(" · ", dto.Details
        .Where(pair => !string.Equals(pair.Key, "application", StringComparison.Ordinal))
        .Select(pair => $"{pair.Key}: {pair.Value}"));
}
