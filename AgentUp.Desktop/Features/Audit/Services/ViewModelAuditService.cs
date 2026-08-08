namespace AgentUp.Desktop.Features.Audit.Services;

public sealed class ViewModelAuditService : ViewModelAuditor
{
    public ViewModelAuditService(HttpClient http)
        : base(http)
    {
    }
}
