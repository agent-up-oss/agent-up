using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Validation.Headless;

public sealed class ValidationPanelTests
{
    [AvaloniaTest]
    public async Task Validation_sidebar_isCollapsed_whenAnApplicationIsSelected()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([DesktopDomain.WorkspaceWithApplications().Build()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        await app.Content.SelectApplicationTabAsync();

        await HeadlessExtensions.FlushAsync();

        Assert.That(() => viewModel.Git.IsLoading, Is.False.After(1000).PollEvery(20));
        Assert.That(viewModel.Git.ErrorMessage, Is.Null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowValidation, Is.True);
            Assert.That(viewModel.IsValidationOpen, Is.False);
            Assert.That(viewModel.Validation!.IsCollapsed, Is.True);
            Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.IsVisible, Is.True);
            Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.Width, Is.EqualTo(56));
            Assert.That(app.Window.FindControl<Button>("ValidationToggleCollapsed"), Is.Not.Null);
        });
    }

    [AvaloniaTest]
    public async Task Validation_sidebar_expandsFromItsCollapsedRailToggle()
    {
        var app = await AppDriver.LaunchWithWorkspacesAsync([DesktopDomain.WorkspaceWithApplications().Build()]);
        var viewModel = (MainViewModel)app.Window.DataContext!;
        await app.Content.SelectApplicationTabAsync();
        var toggle = app.Window.FindControl<Button>("ValidationToggleCollapsed")!;

        await app.Window.ClickControlAsync(toggle);
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Validation!.IsCollapsed, Is.False);
            Assert.That(viewModel.IsValidationOpen, Is.True);
            Assert.That(app.Window.FindControl<Border>("ValidationPanel")!.Width, Is.EqualTo(360));
        });
    }
}
