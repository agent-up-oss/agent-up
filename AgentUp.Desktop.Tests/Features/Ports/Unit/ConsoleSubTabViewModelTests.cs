using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Ports.Unit;

[TestFixture]
public sealed class ConsoleSubTabViewModelTests
{
    [Test]
    public void Console_subtab_usesTheConsoleAutomationId()
    {
        var tab = new ConsoleSubTabViewModel();

        Assert.That(tab.Label, Is.EqualTo("Console"));
        Assert.That(tab.AutomationId, Is.EqualTo(DesktopAutomationIds.AppSubtabConsole));
    }

    [Test]
    public void Diagnostics_subtab_usesTheDiagnosticsAutomationId()
    {
        var tab = new AuditSubTabViewModel();

        Assert.That(tab.Label, Is.EqualTo("Diagnostics"));
        Assert.That(tab.AutomationId, Is.EqualTo(DesktopAutomationIds.AppSubtabDiagnostics));
    }
}
