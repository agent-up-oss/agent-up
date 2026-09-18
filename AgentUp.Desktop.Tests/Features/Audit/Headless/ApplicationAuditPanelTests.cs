using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Ports.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Views;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Audit.Headless;

[TestFixture]
public sealed class ApplicationAuditPanelTests
{
    [AvaloniaTest]
    public async Task AuditTab_isLabelledDiagnostics()
    {
        var (_, viewModel) = await OpenAuditTabAsync();

        Assert.That(AuditTab(viewModel).Label, Is.EqualTo("Diagnostics"));
    }

    [AvaloniaTest]
    public async Task AuditTab_showsTheEventListAndItsControls()
    {
        var (app, _) = await OpenAuditTabAsync();

        Assert.Multiple(() =>
        {
            Assert.That(app.Window.FindControl<Grid>("AuditPanel")!.IsVisible, Is.True);
            Assert.That(app.Window.FindControl<ItemsControl>("AuditEventList"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("AuditStreamingButton")!.Classes.Contains("au-button"), Is.True);
            Assert.That(app.Window.FindControl<Button>("AuditRefreshButton")!.Classes.Contains("au-button"), Is.True);
        });
    }

    [AvaloniaTest]
    public async Task AuditTab_showsEveryPagingControl()
    {
        var (app, _) = await OpenAuditTabAsync();

        string[] paging =
            ["AuditFirstPageButton", "AuditPreviousPageButton", "AuditNextPageButton", "AuditLastPageButton"];

        var missing = paging.Where(name => app.Window.FindControl<Button>(name) is null).ToArray();
        Assert.That(missing, Is.Empty);
    }

    [AvaloniaTest]
    public async Task AuditTab_opensStreamingLiveAndReadyToRefresh()
    {
        var (_, viewModel) = await OpenAuditTabAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Audit.StreamingButtonText, Is.EqualTo("Streaming Live"));
            Assert.That(viewModel.Audit.CanRefresh, Is.True);
            Assert.That(viewModel.Audit.PageJumpButtons, Has.Count.EqualTo(1));
        });
    }

    private static AuditSubTabViewModel AuditTab(MainViewModel viewModel)
        => viewModel.SubTabs.OfType<AuditSubTabViewModel>().Single();

    private static async Task<(AppDriver App, MainViewModel ViewModel)> OpenAuditTabAsync()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([DesktopDomain.WorkspaceWithApplications().Build()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Application;
        viewModel.SelectedSubTab = AuditTab(viewModel);
        await HeadlessExtensions.FlushAsync();
        return (app, viewModel);
    }
}
