using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Features.Files.Headless;

[TestFixture]
public sealed class FilesPanelBehaviorTests
{
    [AvaloniaTest]
    public async Task The_Files_tab_sits_after_Git_and_reads_nothing_until_it_is_opened()
    {
        var (driver, handler) = await AppDriver.LaunchWithFakeHttpAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;

        var labels = viewModel.ShellTabs.Select(tab => tab.Label).ToArray();

        Assert.That(labels, Is.EqualTo(new[] { "Overview", "Agent", "Git", "Files", "Settings" }));
        Assert.That(driver.Window.FindControl<Border>("FilesPanel")!.IsVisible, Is.False);
        Assert.That(handler.WorkspaceFiles.ListedFolders, Is.Empty);
    }

    [AvaloniaTest]
    public async Task Opening_Files_shows_the_top_level_folded()
    {
        var (driver, handler) = await AppDriver.LaunchWithFakeHttpAsync(DesktopDomain.Workspace().Build());
        var viewModel = await OpenFilesAsync(driver);

        Assert.That(driver.Window.FindControl<Border>("FilesPanel")!.IsVisible, Is.True);
        Assert.That(viewModel.Files.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "apps", "README.md" }));
        Assert.That(handler.WorkspaceFiles.ListedFolders, Is.EqualTo(new[] { "" }));
        Assert.That(viewModel.Files.Nodes[0].ChangedLabel, Is.EqualTo("1"));
    }

    [AvaloniaTest]
    public async Task Clicking_a_folder_toggle_unfolds_it_from_the_Server()
    {
        var (driver, handler) = await AppDriver.LaunchWithFakeHttpAsync(DesktopDomain.Workspace().Build());
        var viewModel = await OpenFilesAsync(driver);

        await driver.Window.ClickControlAsync(Find<Button>(driver, DesktopAutomationIds.FilesToggle("apps")));
        await WaitAsync(() => viewModel.Files.Nodes.Count > 2);

        Assert.That(viewModel.Files.Nodes.Select(node => node.Path), Is.EqualTo(new[] { "apps", "apps/api", "apps/storefront", "README.md" }));
        Assert.That(handler.WorkspaceFiles.ListedFolders, Is.EqualTo(new[] { "", "apps" }));
    }

    [AvaloniaTest]
    public async Task Opening_a_file_shows_it_as_a_page_and_Back_returns_to_the_tree()
    {
        var (driver, _) = await AppDriver.LaunchWithFakeHttpAsync(DesktopDomain.Workspace().Build());
        var viewModel = await OpenFilesAsync(driver);

        await driver.Window.ClickControlAsync(Find<Button>(driver, DesktopAutomationIds.FilesOpen(DesktopDomain.FileReadme)));
        await WaitAsync(() => viewModel.Files.Editor.HasContent);
        var editorText = driver.Window.FindControl<TextBox>("FileEditorText")!;
        var page = driver.Window.FindControl<Border>("FileEditorPage")!;

        Assert.That(page.IsVisible, Is.True);
        Assert.That(editorText.Text, Is.EqualTo("# Harbor Shop\n"));
        Assert.That(editorText.IsReadOnly, Is.False);
        editorText.CaretIndex = 4;
        await HeadlessExtensions.FlushAsync();
        Assert.That(viewModel.Files.Editor.CaretLabel, Is.EqualTo("Ln 1, Col 5"), "moving the caret in the editor moves the footer position");

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("FileEditorBackButton")!);
        await HeadlessExtensions.FlushAsync();

        Assert.That(page.IsVisible, Is.False);
        Assert.That(viewModel.Files.IsTreeVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task Editing_and_saving_writes_the_file_to_the_Server()
    {
        var (driver, handler) = await AppDriver.LaunchWithFakeHttpAsync(DesktopDomain.Workspace().Build());
        var viewModel = await OpenFilesAsync(driver);
        await driver.Window.ClickControlAsync(Find<Button>(driver, DesktopAutomationIds.FilesOpen(DesktopDomain.FileReadme)));
        await WaitAsync(() => viewModel.Files.Editor.HasContent);

        driver.Window.FindControl<TextBox>("FileEditorText")!.Text = "# Harbor Shop\n\nEdited by hand.\n";
        await HeadlessExtensions.FlushAsync();
        var save = driver.Window.FindControl<Button>("FileEditorSaveButton")!;
        Assert.That(save.IsEffectivelyEnabled, Is.True);
        await driver.Window.ClickControlAsync(save);
        await WaitAsync(() => handler.WorkspaceFiles.Saves == 1 && !viewModel.Files.Editor.IsSaving);

        Assert.That(handler.WorkspaceFiles.Files[DesktopDomain.FileReadme], Does.Contain("Edited by hand."));
        Assert.That(viewModel.Files.Editor.StateLabel, Is.EqualTo("Saved"));
        Assert.That(viewModel.Files.Editor.StatusLabel, Is.EqualTo("Modified"));
    }

    [AvaloniaTest]
    public async Task A_save_the_Server_refuses_as_stale_shows_Reload()
    {
        var (driver, handler) = await AppDriver.LaunchWithFakeHttpAsync(DesktopDomain.Workspace().Build());
        var viewModel = await OpenFilesAsync(driver);
        await driver.Window.ClickControlAsync(Find<Button>(driver, DesktopAutomationIds.FilesOpen(DesktopDomain.FileReadme)));
        await WaitAsync(() => viewModel.Files.Editor.HasContent);
        handler.WorkspaceFiles.RefuseNextSaveAsStale = true;

        driver.Window.FindControl<TextBox>("FileEditorText")!.Text = "mine";
        await HeadlessExtensions.FlushAsync();
        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("FileEditorSaveButton")!);
        await WaitAsync(() => viewModel.Files.Editor.IsConflict);
        await HeadlessExtensions.FlushAsync();

        Assert.That(Find<Button>(driver, DesktopAutomationIds.FileEditorReload).IsEffectivelyVisible, Is.True);
        Assert.That(handler.WorkspaceFiles.Saves, Is.Zero);
    }

    private static async Task<MainViewModel> OpenFilesAsync(AppDriver driver)
    {
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        await driver.Window.ClickControlAsync(Find<Control>(driver, DesktopAutomationIds.ShellTabFiles));
        await WaitAsync(() => viewModel.Files.Nodes.Count > 0);
        await HeadlessExtensions.FlushAsync();
        return viewModel;
    }

    private static T Find<T>(AppDriver driver, string automationId) where T : Control
        => driver.Window.GetVisualDescendants().OfType<T>()
               .FirstOrDefault(control => Avalonia.Automation.AutomationProperties.GetAutomationId(control) == automationId)
           ?? throw new AssertionException($"No {typeof(T).Name} with automation id {automationId}.");

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await HeadlessExtensions.FlushAsync();
            await Task.Delay(10);
        }
    }
}
