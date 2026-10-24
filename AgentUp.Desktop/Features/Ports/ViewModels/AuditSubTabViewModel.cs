using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Ports.ViewModels;

public sealed class AuditSubTabViewModel : SubTabViewModel
{
    public override string Label => "Diagnostics";

    public override string AutomationId => DesktopAutomationIds.AppSubtabDiagnostics;
}
