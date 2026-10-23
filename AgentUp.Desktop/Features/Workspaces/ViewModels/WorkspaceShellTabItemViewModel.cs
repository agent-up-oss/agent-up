using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceShellTabItemViewModel(WorkspaceShellTab kind, string label)
{
    public WorkspaceShellTab Kind { get; } = kind;

    public string Label { get; } = label;

    public string AutomationId => Kind switch
    {
        WorkspaceShellTab.Overview => DesktopAutomationIds.ShellTabOverview,
        WorkspaceShellTab.Agent => DesktopAutomationIds.ShellTabAgent,
        WorkspaceShellTab.Git => DesktopAutomationIds.ShellTabGit,
        _ => $"shell.tab.{Kind}".ToLowerInvariant()
    };
}
