using Avalonia.Controls;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class SidebarDriver(MainWindow window)
{
    private MainViewModel Vm => (MainViewModel)window.DataContext!;

    public bool IsExpanded => Vm.IsSidebarExpanded;
    public bool IsCollapsed => Vm.IsSidebarCollapsed;

    public int WorkspaceCount => Vm.Workspaces.Count;

    public bool IsShowingNames =>
        window.FindControl<ScrollViewer>("WorkspaceListExpanded")?.IsVisible ?? false;

    public bool IsShowingAvatars =>
        window.FindControl<ScrollViewer>("WorkspaceListCollapsed")?.IsVisible ?? false;

    public string? SelectedWorkspaceName => Vm.SelectedWorkspace?.DisplayName;

    public async Task CollapseAsync()
    {
        if (Vm.IsSidebarCollapsed) return;
        var toggle = window.FindControl<Button>("SidebarToggle")!;
        await window.ClickControlAsync(toggle);
    }

    public async Task ExpandAsync()
    {
        if (Vm.IsSidebarExpanded) return;
        var toggle = window.FindControl<Button>("SidebarToggle")!;
        await window.ClickControlAsync(toggle);
    }

    public async Task SelectWorkspaceAtIndexAsync(int index)
    {
        Vm.SelectedWorkspace = Vm.Workspaces[index];
        await HeadlessExtensions.FlushAsync();
    }

    public async Task ClickReloadAsync()
    {
        var button = window.FindControl<Button>("ReloadButton");
        if (button is null)
            throw new InvalidOperationException("ReloadButton not found — sidebar must be expanded to use this overload.");
        await window.ClickControlAsync(button);
    }
}
