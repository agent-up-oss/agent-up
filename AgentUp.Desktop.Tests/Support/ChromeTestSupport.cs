using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Support;

internal static class ChromeTestSupport
{
    public static T? FindDescendantByName<T>(Control root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));

    public static T? FindDescendantByAutomationId<T>(Control root, string automationId) where T : Control
        => root.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(control => string.Equals(
                AutomationProperties.GetAutomationId(control),
                automationId,
                StringComparison.Ordinal));
}
