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

    public bool PortPaneShowsError =>
        window.FindControl<Border>("WebViewErrorBanner")?.IsVisible ?? false;

    public string? WebViewErrorMessage =>
        window.FindControl<TextBlock>("WebViewErrorText")?.Text;

    public bool ShowsAddressNavBar =>
        window.FindControl<Border>("AddressNavBar")?.IsVisible ?? false;

    public bool ShowsFirstRunTutorial =>
        window.FindControl<Panel>("FirstRunTutorialOverlay")?.IsVisible ?? false;

    public string? TutorialStatusMessage =>
        window.FindControl<TextBlock>("TutorialStatusMessage")?.Text;

    public string? AddressBarText =>
        window.FindControl<TextBox>("AddressBar")?.Text;

    public bool ShowsBrowserBackButton =>
        window.FindControl<Button>("BrowserBackButton")?.IsVisible ?? false;

    public bool ShowsBrowserForwardButton =>
        window.FindControl<Button>("BrowserForwardButton")?.IsVisible ?? false;

    public bool ShowsBrowserReloadButton =>
        window.FindControl<Button>("BrowserReloadButton")?.IsVisible ?? false;

    public bool AddressBarIsFocused =>
        window.FindControl<TextBox>("AddressBar")?.IsFocused ?? false;

    public async Task FocusAddressBarAsync()
    {
        window.FindControl<TextBox>("AddressBar")?.Focus();
        await HeadlessExtensions.FlushAsync();
    }

    public async Task ClickWorkspaceDetailAsync()
    {
        var detail = window.FindControl<Border>("WorkspaceDetail")
            ?? throw new InvalidOperationException("Workspace detail was not found.");
        await window.ClickControlAsync(detail);
    }

    public async Task ClickSkipTutorialAsync()
    {
        var button = window.FindControl<Button>("SkipTutorialButton")
            ?? throw new InvalidOperationException("Skip tutorial button was not found.");
        await window.ClickControlAsync(button);
    }

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
