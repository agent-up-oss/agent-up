using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.ViewModels;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;

namespace AgentUp.Desktop.Tests.Features.Git.Headless;

[TestFixture]
public sealed class GitPanelBehaviorTests
{
    [AvaloniaTest]
    public async Task GitPanel_isHiddenUntilTheGitTabIsSelected()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        var panel = driver.Window.FindControl<Border>("GitPanel")!;

        Assert.That(panel.IsVisible, Is.False);

        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        await HeadlessExtensions.FlushAsync();

        Assert.That(panel.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task GitPanel_showsTheCommitMessageBoxAndCommitButton()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        await HeadlessExtensions.FlushAsync();

        Assert.That(driver.Window.FindControl<TextBox>("GitCommitMessage")!.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<Button>("GitCommitButton")!.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<Button>("GitDiscardButton")!.IsVisible, Is.False);
        Assert.That(driver.Window.FindControl<Button>("GitForcePushButton"), Is.Null);
        Assert.That(driver.Window.FindControl<Button>("GitHistoryButton")!.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<Grid>("GitConfirmOverlay")!.IsVisible, Is.False);
        Assert.That(driver.Window.FindControl<ItemsControl>("GitLog")!.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public async Task GitCommitButton_staysDisabledWithoutSelectedFilesOrAMessage()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        await HeadlessExtensions.FlushAsync();

        Assert.That(driver.Window.FindControl<Button>("GitCommitButton")!.IsEffectivelyEnabled, Is.False);
    }

    [AvaloniaTest]
    public async Task GitPanel_opensHistoryFromTheHistoryButton()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        Assert.That(() => viewModel.Git.IsLoading, Is.False.After(1000).PollEvery(20));
        await HeadlessExtensions.FlushAsync();

        var history = driver.Window.FindControl<Button>("GitHistoryButton")!;
        Assert.That(history.IsEffectivelyEnabled, Is.True);
        await driver.Window.ClickControlAsync(history);
        await HeadlessExtensions.FlushAsync();

        Assert.That(viewModel.Git.IsHistoryOpen, Is.True);
        Assert.That(driver.Window.FindControl<ItemsControl>("GitLog")!.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<Button>("GitHistoryBackButton")!.IsVisible, Is.True);

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("GitHistoryBackButton")!);
        await HeadlessExtensions.FlushAsync();

        Assert.That(viewModel.Git.IsHistoryOpen, Is.False);
        Assert.That(driver.Window.FindControl<ItemsControl>("GitLog")!.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public async Task GitPanel_rendersServerOwnedProposalMessagesAndState()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        viewModel.Git.ApplyQueue(new CommitQueueDto(
            [new CommitQueueEntryDto("Commits", "feat(Commits): queue", ["a.cs"], "entry-1", "base", "tip", "ready")],
            [], "/managed/queue", "base", "tip", 2));
        await HeadlessExtensions.FlushAsync();

        var queue = driver.Window.FindControl<Border>("CommitProposalQueue")!;
        Assert.That(queue.IsVisible, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(queue.Background, Is.Not.Null, "The queue should resolve its theme-aware surface brush.");
            Assert.That(queue.BorderBrush, Is.Not.Null, "The queue should resolve its theme-aware border brush.");
        });
        Assert.That(queue.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "feat(Commits): queue"), Is.True);
        Assert.That(queue.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "ready"), Is.True);
    }

    [AvaloniaTest]
    public async Task GitPanel_hidesHostingUntilAForgeIsEnabled()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        await HeadlessExtensions.FlushAsync();

        var strip = driver.Window.FindControl<Border>("GitHostingStrip")!;
        Assert.That(strip.IsVisible, Is.False);

        viewModel.Git.ApplyHosting(new ForgeHostingDto(
            "ready",
            "github",
            new ForgeProposalDto("1", "42", "Ready", "https://example.test/42", "topic", "main", "open"),
            [new ForgeCheckDto("ci", "completed", "success", "https://example.test/check")],
            [new ForgeReviewThreadDto("PRRT_1", "https://example.test/thread", "nits", false)],
            new ForgeMergeabilityDto(true, "clean"),
            null));
        await HeadlessExtensions.FlushAsync();

        Assert.That(strip.IsVisible, Is.True);
        Assert.That(strip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Ready"), Is.True);
        Assert.That(strip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Checks passed"), Is.True);
    }

    [AvaloniaTest]
    public async Task GitPanel_showsDistinctCopyForMissingCredentialAndUnreachable()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;

        viewModel.Git.ApplyHosting(new ForgeHostingDto("needsCredential", "github", null, [], [], null, null));
        await HeadlessExtensions.FlushAsync();
        var strip = driver.Window.FindControl<Border>("GitHostingStrip")!;
        Assert.That(strip.IsVisible, Is.True);
        Assert.That(strip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text!.Contains("Sign in")), Is.True);

        viewModel.Git.ApplyHosting(new ForgeHostingDto("unreachable", "github", null, [], [], null, "Could not reach the host."));
        await HeadlessExtensions.FlushAsync();
        Assert.That(strip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Could not reach the host."), Is.True);

        viewModel.Git.ApplyHosting(new ForgeHostingDto("noProposal", "github", null, [], [], null, null));
        await HeadlessExtensions.FlushAsync();
        Assert.That(strip.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text!.Contains("No open proposal")), Is.True);
    }

    [AvaloniaTest]
    public async Task GitFileDiffOverlay_isHiddenUntilAFileIsOpened()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var overlay = driver.Window.FindControl<Grid>("GitFileDiffOverlay")!;

        Assert.That(overlay.IsVisible, Is.False);

        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.Git.Diff.ShowDiff("src/main.cs", "Modified", "@@ -1 +1 @@");
        await HeadlessExtensions.FlushAsync();

        Assert.That(overlay.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<TextBlock>("GitFileDiffPath")!.Text, Is.EqualTo("src/main.cs"));
        Assert.That(driver.Window.FindControl<ListBox>("GitFileDiffLines"), Is.Not.Null);

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("GitFileDiffDismissButton")!);

        Assert.That(overlay.IsVisible, Is.False);
    }

    [AvaloniaTest]
    public async Task GitPanel_showsDiscardNextToPushOnlyWhenFilesAreSelected()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        Assert.That(() => viewModel.Git.IsLoading, Is.False.After(1000).PollEvery(20));
        Assert.That(() => viewModel.Git.FileCount, Is.EqualTo(0).After(500).PollEvery(20));
        await HeadlessExtensions.FlushAsync();

        var discard = driver.Window.FindControl<Button>("GitDiscardButton")!;
        Assert.That(discard.IsVisible, Is.False);

        var file = new GitChangeNodeViewModel("orders.ts", "apps/api/orders.ts", 2, false, "Modified");
        file.SetHost(viewModel.Git);
        viewModel.Git.Nodes.Add(file);
        file.IsSelected = true;
        await HeadlessExtensions.FlushAsync();

        Assert.That(viewModel.Git.HasSelectedFiles, Is.True);
        Assert.That(discard.IsVisible, Is.True);
    }

    [AvaloniaTest]
    public async Task GitPanel_opensThePushConfirmOverlayFromPush()
    {
        var driver = await AppDriver.LaunchWithWorkspaceAsync(DesktopDomain.Workspace().Build());
        var viewModel = (MainViewModel)driver.Window.DataContext!;
        viewModel.SelectedShellTab = WorkspaceShellTab.Git;
        Assert.That(() => viewModel.Git.IsLoading, Is.False.After(1000).PollEvery(20));
        await HeadlessExtensions.FlushAsync();

        var overlay = driver.Window.FindControl<Grid>("GitConfirmOverlay")!;
        Assert.That(overlay.IsVisible, Is.False);

        await driver.Window.ClickControlAsync(driver.Window.FindControl<Button>("GitPushButton")!);
        await HeadlessExtensions.FlushAsync();

        Assert.That(viewModel.Git.IsConfirmingPush, Is.True);
        Assert.That(overlay.IsVisible, Is.True);
        Assert.That(driver.Window.FindControl<TextBlock>("GitConfirmTitle")!.Text, Does.Contain("Push"));
    }
}
