using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class ValidationPanelTests
{
    [AvaloniaTest]
    public async Task Validation_sidebar_isOpen_forTheSelectedWorkspace()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([WorkspaceFixtures.WithApplications()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;

        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsValidationOpen, Is.True);
            Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.IsVisible, Is.True);
            Assert.That(app.Window.FindControl<ItemsControl>("ValidationFlowList"), Is.Not.Null);
            Assert.That(app.Window.FindControl<Button>("ValidationToggle"), Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public async Task Validation_sidebar_collapsesFromItsHeaderToggle()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([WorkspaceFixtures.WithApplications()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        var toggle = app.Window.FindControl<Button>("ValidationToggle")!;

        await app.Window.ClickControlAsync(toggle);
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Validation!.IsCollapsed, Is.True);
            Assert.That(viewModel.IsValidationOpen, Is.False);
            Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.Width, Is.EqualTo(56));
        });
    }
}
