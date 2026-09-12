using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace AgentUp.Desktop.Tests.Features.Git.Headless;

[TestFixture]
public sealed class GitPanelBehaviorTests
{
    [AvaloniaTest]
    public async Task GitPanel_isHiddenUntilTheChromeToggleIsClicked()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());
        var panel = driver.Window.FindControl<Border>("GitPanel")!;

        Assert.That(panel.IsVisible, Is.False);

        await driver.Window.ClickControlAsync(ChromeTestSupport.FindDescendantByName<Button>(driver.Window, "GitPanelToggle")!);

        Assert.That(panel.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task GitPanel_showsTheCommitMessageBoxAndCommitButton()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());
        await driver.Window.ClickControlAsync(ChromeTestSupport.FindDescendantByName<Button>(driver.Window, "GitPanelToggle")!);

        Assert.That(driver.Window.FindControl<TextBox>("GitCommitMessage")!.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<Button>("GitCommitButton")!.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<Button>("GitDiscardButton")!.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task GitCommitButton_staysDisabledWithoutSelectedFilesOrAMessage()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());
        await driver.Window.ClickControlAsync(ChromeTestSupport.FindDescendantByName<Button>(driver.Window, "GitPanelToggle")!);

        Assert.That(driver.Window.FindControl<Button>("GitCommitButton")!.IsEffectivelyEnabled, Is.False);
    }

    [AvaloniaTest]
    public async Task GitFileDiffOverlay_isHiddenUntilAFileIsOpened()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(WorkspaceFixtures.Single());
        var overlay = driver.Window.FindControl<Grid>("GitFileDiffOverlay")!;

        Assert.That(overlay.IsVisible, Is.False);

        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.Git.Diff.ShowDiff("src/main.cs", "Modified", "@@ -1 +1 @@");
        await HeadlessExtensions.FlushAsync();

        Assert.That(overlay.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<TextBlock>("GitFileDiffPath")!.Text, Is.EqualTo("src/main.cs"));

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("GitFileDiffDismissButton")!);

        Assert.That(overlay.IsVisible, Is.False);
    }
}
