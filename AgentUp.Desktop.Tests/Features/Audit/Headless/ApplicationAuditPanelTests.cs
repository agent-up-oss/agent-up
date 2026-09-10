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
    public async Task AuditTab_ShowsStreamingControlsAndPagination()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([WorkspaceFixtures.WithApplications()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        var auditTab = viewModel.SubTabs.OfType<AuditSubTabViewModel>().Single();
        Assert.That(auditTab.Label, Is.EqualTo("Diagnostics"));

        viewModel.SelectedSubTab = auditTab;
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(app.Window.FindControl<Grid>("AuditPanel")!.IsVisible, Is.True);
            Assert.That(app.Window.FindControl<ItemsControl>("AuditEventList"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("AuditFirstPageButton"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("AuditPreviousPageButton"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("AuditNextPageButton"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("AuditLastPageButton"), Is.Not.Null);
            Assert.That(viewModel.Audit.StreamingButtonText, Is.EqualTo("Streaming Live"));
            Assert.That(viewModel.Audit.CanRefresh, Is.True);
            Assert.That(viewModel.Audit.PageJumpButtons, Has.Count.EqualTo(1));
        });
    }
}
