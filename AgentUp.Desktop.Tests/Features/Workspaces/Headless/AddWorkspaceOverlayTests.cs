using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Headless;

[TestFixture]
public sealed class AddWorkspaceOverlayTests
{
    [AvaloniaTest]
    public async Task AddWorkspaceButton_opensTheRepositoryAndBranchDialog()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());
        var overlay = driver.Window.FindControl<Grid>("AddWorkspaceOverlay")!;

        Assert.That(overlay.IsVisible, Is.False);

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("AddWorkspaceButton")!);

        Assert.That(overlay.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<TextBox>("AddWorkspaceRepository")!.Text, Is.Empty);
        Assert.That(driver.Window.FindControl<TextBox>("AddWorkspaceBranch")!.Text, Is.EqualTo("main"));
    }

    [AvaloniaTest]
    public async Task AddWorkspaceCancelButton_closesTheDialog()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());
        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("AddWorkspaceButton")!);

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("AddWorkspaceCancelButton")!);

        Assert.That(driver.Window.FindControl<Grid>("AddWorkspaceOverlay")!.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public async Task ConfirmingTheDialogClonesTheRepositoryAndSelectsTheNewWorkspace()
    {
        var (driver, _) = await AppDriver.LaunchWithMutableWorkspacesAsync([WorkspaceFixtures.Single()]);
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("AddWorkspaceButton")!);

        driver.Window.FindControl<TextBox>("AddWorkspaceRepository")!.Text = "https://example.test/acme/widgets.git";
        await HeadlessExtensions.FlushAsync();

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("AddWorkspaceConfirmButton")!);
        await HeadlessExtensions.FlushAsync();

        Assert.That(driver.Window.FindControl<Grid>("AddWorkspaceOverlay")!.IsVisible, Is.False);
        Assert.That(viewModel.Sidebar.Workspaces.Select(workspace => workspace.DisplayName), Does.Contain("widgets"));
        Assert.That(viewModel.Sidebar.SelectedWorkspace!.DisplayName, Is.EqualTo("widgets"));
    }

    [AvaloniaTest]
    public async Task FailingCloneKeepsTheDialogOpenWithTheServerMessage()
    {
        var driver = await AppDriver.LaunchWithServerErrorAsync();
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.Sidebar.AddWorkspace.Show();
        viewModel.Sidebar.AddWorkspace.Repository = "https://example.test/acme/widgets.git";

        await viewModel.Sidebar.CloneWorkspaceAsync("https://example.test/acme/widgets.git", "main");
        await HeadlessExtensions.FlushAsync();

        Assert.That(driver.Window.FindControl<Grid>("AddWorkspaceOverlay")!.IsVisible, Is.True);
        Assert.That(viewModel.Sidebar.AddWorkspace.ErrorMessage, Is.Not.Null);
        Assert.That(driver.Window.FindControl<TextBlock>("AddWorkspaceError")!.IsVisible, Is.True);
    }
}
