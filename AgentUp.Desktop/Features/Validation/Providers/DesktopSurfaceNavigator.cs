using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class DesktopSurfaceNavigator
{
    public string? Navigate(MainViewModel viewModel, string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || !DesktopNamedRoutes.IsNamedRoute(route))
            return $"Desktop Navigate steps require a named surface route such as workspaces/list or workspace/git.";

        switch (route)
        {
            case DesktopNamedRoutes.Login:
                viewModel.Login.ShowPicker();
                return null;
            case DesktopNamedRoutes.WorkspacesList:
                return viewModel.Login.IsVisible
                    ? "Timed out waiting for workspaces/list (login is still visible)."
                    : null;
            case DesktopNamedRoutes.WorkspaceOverview:
                return SelectShell(viewModel, WorkspaceShellTab.Overview);
            case DesktopNamedRoutes.WorkspaceApplications:
                return SelectShell(viewModel, WorkspaceShellTab.Application);
            case DesktopNamedRoutes.WorkspaceGit:
                return SelectShell(viewModel, WorkspaceShellTab.Git);
            case DesktopNamedRoutes.WorkspaceAgents:
                return SelectShell(viewModel, WorkspaceShellTab.Agent);
            default:
                return $"Unknown Desktop surface route '{route}'.";
        }
    }

    private static string? SelectShell(MainViewModel viewModel, WorkspaceShellTab tab)
    {
        if (viewModel.Login.IsVisible)
            return $"Timed out waiting for {tab} (login is still visible).";

        viewModel.SelectedShellTab = tab;
        return null;
    }
}
