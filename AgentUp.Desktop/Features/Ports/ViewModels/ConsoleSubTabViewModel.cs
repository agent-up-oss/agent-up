using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Ports.ViewModels;

public sealed class ConsoleSubTabViewModel : SubTabViewModel
{
    public override string Label => "Console";

    public override string AutomationId => DesktopAutomationIds.AppSubtabConsole;
}
