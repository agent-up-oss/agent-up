using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

public sealed class WorkspaceShellTabItemViewModelTests
{
    [TestCase(WorkspaceShellTab.Overview, DesktopAutomationIds.ShellTabOverview)]
    [TestCase(WorkspaceShellTab.Agent, DesktopAutomationIds.ShellTabAgent)]
    [TestCase(WorkspaceShellTab.Git, DesktopAutomationIds.ShellTabGit)]
    public void Tab_usesTheStableShellAutomationId(WorkspaceShellTab kind, string expected)
    {
        Assert.That(new WorkspaceShellTabItemViewModel(kind, kind.ToString()).AutomationId, Is.EqualTo(expected));
    }

    [Test]
    public void Tab_fallsBackToAKindBasedIdForUnknownTabs() =>
        Assert.That(
            new WorkspaceShellTabItemViewModel((WorkspaceShellTab)99, "Other").AutomationId,
            Is.EqualTo("shell.tab.99"));
}
