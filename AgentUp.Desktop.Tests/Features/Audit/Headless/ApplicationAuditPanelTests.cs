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
    public async Task AuditTab_ShowsNativePaginatedEventList()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([WorkspaceFixtures.WithApplications()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        var auditTab = viewModel.SubTabs.OfType<AuditSubTabViewModel>().Single();

        viewModel.SelectedSubTab = auditTab;
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(app.Window.FindControl<Grid>("AuditPanel")!.IsVisible, Is.True);
            Assert.That(app.Window.FindControl<ListBox>("AuditEventList"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("AuditLoadMoreButton"), Is.Not.Null);
        });
    }
}
