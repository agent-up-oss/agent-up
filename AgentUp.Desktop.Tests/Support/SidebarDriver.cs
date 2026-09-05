using Avalonia.Controls;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class SidebarDriver(MainWindow window)
{
    private MainViewModel Vm => (MainViewModel)window.DataContext!;

    public bool IsExpanded => Vm.Sidebar.IsExpanded;
    public bool IsCollapsed => Vm.Sidebar.IsCollapsed;

    public int WorkspaceCount => Vm.Sidebar.Workspaces.Count;

    public bool IsShowingNames =>
        window.FindControl<ScrollViewer>("WorkspaceListExpanded")?.IsVisible ?? false;

    public bool IsShowingAvatars =>
        window.FindControl<ScrollViewer>("WorkspaceListCollapsed")?.IsVisible ?? false;

    public string? SelectedWorkspaceName => Vm.Sidebar.SelectedWorkspace?.DisplayName;

    public IReadOnlyList<string> ExpandedWorkspaceTexts =>
        window.FindControl<ScrollViewer>("WorkspaceListExpanded")
            ?.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .Where(text => text.Length > 0)
            .ToList()
        ?? [];

    public IReadOnlyList<string> WorkspaceIds =>
        Vm.Sidebar.Workspaces.Select(workspace => workspace.Id).ToList();

    public async Task CollapseAsync()
    {
        if (Vm.Sidebar.IsCollapsed) return;
        var toggle = window.FindControl<Button>("SidebarToggle")!;
        await window.ClickControlAsync(toggle);
    }

    public async Task ExpandAsync()
    {
        if (Vm.Sidebar.IsExpanded) return;
        var toggle = window.FindControl<Button>("SidebarToggle")!;
        await window.ClickControlAsync(toggle);
    }

    public async Task SelectWorkspaceAtIndexAsync(int index)
    {
        Vm.Sidebar.SelectedWorkspace = Vm.Sidebar.Workspaces[index];
        await HeadlessExtensions.FlushAsync();
    }

    public async Task ClickReloadAsync()
    {
        var button = window.FindControl<Button>("ReloadButton");
        if (button is null)
            throw new InvalidOperationException("ReloadButton not found — sidebar must be expanded to use this overload.");
        await window.ClickControlAsync(button);
    }

    public async Task ClickStartOnWorkspaceAtIndexAsync(int index)
    {
        var item = Vm.Sidebar.Workspaces[index];
        await window.ClickControlAsync(FindLifecycleButton("Start workspace", item.DisplayName));
    }

    public async Task ClickStopOnWorkspaceAtIndexAsync(int index)
    {
        var item = Vm.Sidebar.Workspaces[index];
        await window.ClickControlAsync(FindLifecycleButton("Stop workspace", item.DisplayName));
    }

    public async Task ClickDeleteOnWorkspaceAtIndexAsync(int index)
    {
        var item = Vm.Sidebar.Workspaces[index];
        var deleteButton = window.FindControl<ScrollViewer>("WorkspaceListExpanded")
            ?.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button =>
            {
                if (!button.Classes.Contains("wsDeleteButton"))
                    return false;

                Control? entry = button;
                while (entry is not null)
                {
                    if (entry is Border border && border.Classes.Contains("wsEntry"))
                    {
                        return entry.GetVisualDescendants().OfType<TextBlock>()
                            .Any(text => text.Text == item.DisplayName);
                    }

                    entry = entry.GetVisualParent() as Control;
                }

                return false;
            });
        if (deleteButton is null)
            throw new InvalidOperationException($"Delete button for workspace '{item.DisplayName}' was not found.");

        await window.ClickControlAsync(deleteButton);
        await HeadlessExtensions.FlushAsync();
    }

    public bool ShowsDeleteConfirmation =>
        Vm.Sidebar.DeleteConfirmation.IsVisible;

    public bool DeleteOverlayCoversContentOnly =>
        window.FindControl<Grid>("WorkspaceDeleteOverlay")?.GetValue(Grid.RowProperty) is int row && row == 1;

    public async Task ConfirmDeleteAsync()
    {
        var checkbox = window.FindControl<CheckBox>("WorkspaceDeleteConfirmCheckbox")
            ?? throw new InvalidOperationException("Workspace delete confirmation checkbox was not found.");
        checkbox.IsChecked = true;
        await HeadlessExtensions.FlushAsync();

        var button = window.FindControl<Button>("WorkspaceDeleteConfirmButton")
            ?? throw new InvalidOperationException("Workspace delete confirm button was not found.");
        await window.ClickControlAsync(button);
    }

    public async Task CancelDeleteAsync()
    {
        var button = window.FindControl<Button>("WorkspaceDeleteCancelButton")
            ?? throw new InvalidOperationException("Workspace delete cancel button was not found.");
        await window.ClickControlAsync(button);
    }

    public string? WorkspaceStateAtIndex(int index) => Vm.Sidebar.Workspaces[index].State;

    public bool ArePortWebViewsHidden =>
        window.ArePortWebViewsHiddenForTests;

    private Button FindLifecycleButton(string toolTip, string workspaceName)
    {
        var expanded = window.FindControl<ScrollViewer>("WorkspaceListExpanded")
            ?? throw new InvalidOperationException("WorkspaceListExpanded was not found.");

        return expanded.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button =>
            {
                if (!string.Equals(ToolTip.GetTip(button)?.ToString(), toolTip, StringComparison.Ordinal))
                    return false;

                Control? entry = button;
                while (entry is not null)
                {
                    if (entry is Border border && border.Classes.Contains("wsEntry"))
                    {
                        return entry.GetVisualDescendants().OfType<TextBlock>()
                            .Any(block => block.Text == workspaceName);
                    }

                    entry = entry.GetVisualParent() as Control;
                }

                return false;
            })
            ?? throw new InvalidOperationException($"{toolTip} button for workspace '{workspaceName}' was not found.");
    }
}
