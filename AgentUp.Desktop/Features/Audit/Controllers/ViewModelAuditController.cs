using AgentUp.Desktop.Features.Audit.Services;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Audit.Controllers;

public sealed class ViewModelAuditController : IDisposable
{
    private readonly ViewModelAuditor _auditor;

    public ViewModelAuditController(HttpClient http)
    {
        _auditor = new ViewModelAuditor(http);
    }

    public void Attach(MainViewModel vm, Func<IReadOnlyDictionary<string, string>> viewStateCapture)
        => _auditor.Attach(vm, viewStateCapture);

    public void Dispose()
        => _auditor.Dispose();
}
