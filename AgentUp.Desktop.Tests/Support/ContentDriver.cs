using Avalonia.Controls;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;

namespace AgentUp.Desktop.Tests.Support;

internal sealed class ContentDriver(MainWindow window)
{
    private MainViewModel Vm => (MainViewModel)window.DataContext!;

    public bool IsLoading =>
        window.FindControl<TextBlock>("LoadingIndicator")?.IsVisible ?? false;

    public bool ShowsError =>
        window.FindControl<Panel>("ErrorPanel")?.IsVisible ?? false;

    public bool ShowsEmptyState =>
        window.FindControl<StackPanel>("EmptyState")?.IsVisible ?? false;

    public bool ShowsWorkspaceDetail =>
        window.FindControl<Border>("WorkspaceDetail")?.IsVisible ?? false;

    public string? DisplayedWorkspaceName => Vm.Sidebar.SelectedWorkspace?.DisplayName;

    public string? ErrorMessage => Vm.Sidebar.ErrorMessage;

    // ── Application panel ────────────────────────────────────────────────────

    public bool HasApplications =>
        Vm.Applications.Applications.Count > 0;

    public string? SelectedApplicationName => Vm.Applications.SelectedApplication?.Name;

    public IReadOnlyList<string> ConsoleLines => [.. Vm.Console.Lines];

    public async Task SelectApplicationByIndexAsync(int index)
    {
        Vm.Applications.SelectedApplication = Vm.Applications.Applications[index];
        await HeadlessExtensions.FlushAsync();
    }
}
