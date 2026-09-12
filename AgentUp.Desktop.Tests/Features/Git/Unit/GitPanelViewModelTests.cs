using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Interfaces;
using AgentUp.Desktop.Features.Git.Services;
using AgentUp.Desktop.Features.Git.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Git.Unit;

[TestFixture]
public sealed class GitPanelViewModelTests
{
    [Test]
    public async Task LoadAsync_flattensTheTreeWithDirectoriesFirstAndIndentedFiles()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });

        await panel.LoadAsync("ws-1");

        Assert.That(panel.Nodes.Select(node => node.Name),
            Is.EqualTo(new[] { "Changes", "src", "app", "main.cs", "util.cs", "README.md" }));
        Assert.That(panel.Nodes.Select(node => node.Depth),
            Is.EqualTo(new[] { 0, 1, 2, 3, 3, 1 }));
        Assert.That(panel.Nodes[0].IsDirectory, Is.True);
        Assert.That(panel.Nodes[0].Name, Is.EqualTo("Changes"));
        Assert.That(panel.Nodes[5].IsFile, Is.True);
        Assert.That(panel.FileCount, Is.EqualTo(3));
        Assert.That(panel.Branch, Is.EqualTo("main"));
    }

    [Test]
    public async Task SelectingTheChangesRootSelectsEveryFile()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");

        panel.Nodes[0].IsSelected = true;

        Assert.That(panel.Nodes.Where(node => node.IsFile).All(node => node.IsSelected), Is.True);
        Assert.That(panel.SelectedFileCount, Is.EqualTo(3));
    }

    [Test]
    public async Task SelectingADirectorySelectsEveryFileBeneathIt()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");

        panel.Nodes[1].IsSelected = true;

        Assert.That(panel.Nodes[2].IsSelected, Is.True, "nested directory follows its parent");
        Assert.That(panel.Nodes[3].IsSelected, Is.True);
        Assert.That(panel.Nodes[4].IsSelected, Is.True);
        Assert.That(panel.Nodes[5].IsSelected, Is.False, "files outside the directory stay unselected");
        Assert.That(panel.SelectedFileCount, Is.EqualTo(2));
    }

    [Test]
    public async Task DeselectingASingleFileDeselectsItsAncestorDirectories()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");
        panel.Nodes[1].IsSelected = true;

        panel.Nodes[3].IsSelected = false;

        Assert.That(panel.Nodes[1].IsSelected, Is.False);
        Assert.That(panel.Nodes[2].IsSelected, Is.False);
        Assert.That(panel.Nodes[4].IsSelected, Is.True);
        Assert.That(panel.SelectedFileCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SelectingEveryFileInADirectoryChecksTheDirectory()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");

        panel.Nodes[3].IsSelected = true;
        panel.Nodes[4].IsSelected = true;

        Assert.That(panel.Nodes[2].IsSelected, Is.True);
        Assert.That(panel.Nodes[1].IsSelected, Is.True);
    }

    [Test]
    public async Task CommitCommand_isDisabledUntilFilesAndAMessageAreProvided()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");

        Assert.That(await panel.CommitCommand.CanExecute.FirstAsync(), Is.False);

        panel.Nodes[3].IsSelected = true;
        Assert.That(await panel.CommitCommand.CanExecute.FirstAsync(), Is.False);

        panel.CommitMessage = "feat(App): add main";
        Assert.That(await panel.CommitCommand.CanExecute.FirstAsync(), Is.True);
    }

    [Test]
    public async Task CommitCommand_sendsOnlySelectedFilesAndReloadsTheTree()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree() };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");
        panel.Nodes[3].IsSelected = true;
        panel.CommitMessage = "feat(App): add main";

        await panel.CommitCommand.Execute().FirstAsync();

        Assert.That(client.CommittedRequest!.Files, Is.EqualTo(new[] { "src/app/main.cs" }));
        Assert.That(client.CommittedRequest.Message, Is.EqualTo("feat(App): add main"));
        Assert.That(panel.CommitMessage, Is.Empty);
        Assert.That(panel.StatusMessage, Does.Contain("0123456"));
        Assert.That(client.ChangeRequests, Is.EqualTo(2));
    }

    [Test]
    public async Task DiscardCommand_sendsSelectedFilesAndReloadsTheTree()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree() };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");
        panel.Nodes[5].IsSelected = true;

        await panel.DiscardCommand.Execute().FirstAsync();

        Assert.That(client.DiscardedRequest!.Files, Is.EqualTo(new[] { "README.md" }));
        Assert.That(panel.StatusMessage, Does.Contain("Discarded"));
    }

    [Test]
    public async Task CreateBranchCommand_postsTheNewBranchName()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree() };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");
        panel.NewBranchName = "topic";

        await panel.CreateBranchCommand.Execute().FirstAsync();

        Assert.That(client.BranchRequest!.Name, Is.EqualTo("topic"));
        Assert.That(client.BranchRequest.Create, Is.True);
        Assert.That(panel.NewBranchName, Is.Empty);
    }

    [Test]
    public async Task LoadAsync_keepsSelectionForFilesThatStillExist()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree() };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");
        panel.Nodes[5].IsSelected = true;

        await panel.LoadAsync("ws-1", silent: true);

        Assert.That(panel.Nodes[5].IsSelected, Is.True);
        Assert.That(panel.SelectedFileCount, Is.EqualTo(1));
    }

    [Test]
    public async Task LoadAsync_clearsSelectionWhenTheWorkspaceChanges()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree() };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");
        panel.Nodes[5].IsSelected = true;

        await panel.LoadAsync("ws-2");

        Assert.That(panel.Nodes[5].IsSelected, Is.False);
        Assert.That(panel.SelectedFileCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CommitCommand_keepsTheMessageAndReportsServerFailures()
    {
        var client = new FakeGitApiProvider
        {
            Tree = SampleTree(),
            CommitResult = new GitCommitResultDto(true, false, null, "Commit message is required.")
        };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");
        panel.Nodes[3].IsSelected = true;
        panel.CommitMessage = "feat(App): add main";

        await panel.CommitCommand.Execute().FirstAsync();

        Assert.That(panel.ErrorMessage, Is.EqualTo("Commit message is required."));
        Assert.That(panel.CommitMessage, Is.EqualTo("feat(App): add main"));
    }

    [Test]
    public async Task LoadAsync_withoutAWorkspaceClearsThePanel()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");

        await panel.LoadAsync(null);

        Assert.That(panel.Nodes, Is.Empty);
        Assert.That(panel.Branch, Is.Empty);
        Assert.That(panel.ShowEmptyState, Is.True);
    }

    [Test]
    public async Task LoadAsync_reportsServerFailuresWithoutThrowing()
    {
        var panel = CreatePanel(new FakeGitApiProvider { ChangesFailure = "Connection refused" });

        await panel.LoadAsync("ws-1");

        Assert.That(panel.ErrorMessage, Does.Contain("Connection refused"));
        Assert.That(panel.Nodes, Is.Empty);
    }

    [Test]
    public async Task LoadAsync_clearsASilentPollingErrorAfterRecovery()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree(), ChangesFailure = "Connection refused" };
        var panel = CreatePanel(client);

        await panel.LoadAsync("ws-1", silent: true);
        Assert.That(panel.ErrorMessage, Does.Contain("Connection refused"));

        client.ChangesFailure = null;
        await panel.LoadAsync("ws-1", silent: true);

        Assert.That(panel.ErrorMessage, Is.Null);
        Assert.That(panel.FileCount, Is.EqualTo(3));
    }

    [Test]
    public async Task OpeningAFileShowsItsDiffInTheModal()
    {
        var client = new FakeGitApiProvider
        {
            Tree = SampleTree(),
            FileDiff = new GitFileDiffDto("src/app/main.cs", "Modified", false, "@@ -1 +1 @@")
        };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");

        await panel.Nodes[3].OpenCommand.Execute().FirstAsync();

        Assert.That(panel.Diff.IsVisible, Is.True);
        Assert.That(panel.Diff.Path, Is.EqualTo("src/app/main.cs"));
        Assert.That(panel.Diff.Content, Is.EqualTo("@@ -1 +1 @@"));

        await panel.Diff.CloseCommand.Execute().FirstAsync();

        Assert.That(panel.Diff.IsVisible, Is.False);
    }

    [Test]
    public async Task OpeningADirectoryDoesNotOpenTheDiffModal()
    {
        var panel = CreatePanel(new FakeGitApiProvider { Tree = SampleTree() });
        await panel.LoadAsync("ws-1");

        await panel.Nodes[1].OpenCommand.Execute().FirstAsync();

        Assert.That(panel.Diff.IsVisible, Is.False);
    }

    [Test]
    public async Task OpeningABinaryFileExplainsThatNoTextDiffIsRendered()
    {
        var client = new FakeGitApiProvider
        {
            Tree = SampleTree(),
            FileDiff = new GitFileDiffDto("src/app/main.cs", "Modified", true, "Binary files differ")
        };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");

        await panel.Nodes[3].OpenCommand.Execute().FirstAsync();

        Assert.That(panel.Diff.Content, Does.Contain("binary"));
    }

    [Test]
    public async Task ToggleCommand_flipsPanelVisibility()
    {
        var panel = CreatePanel(new FakeGitApiProvider());

        Assert.That(panel.IsVisible, Is.False);
        Assert.That(panel.ToggleIcon, Is.EqualTo("‹"));

        await panel.ToggleCommand.Execute().FirstAsync();

        Assert.That(panel.IsVisible, Is.True);
        Assert.That(panel.ToggleIcon, Is.EqualTo("›"));
    }

    [Test]
    public async Task LoadAsync_discardsATreeThatArrivesAfterAnotherWorkspaceWasSelected()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree(), HoldChanges = true };
        var panel = CreatePanel(client);
        var stale = panel.LoadAsync("ws-1");

        client.HoldChanges = false;
        client.Tree = OtherWorkspaceTree();
        await panel.LoadAsync("ws-2");

        client.CompleteHeldChanges(SampleTree());
        await stale;

        Assert.That(panel.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "Changes", "other.cs" }));
        Assert.That(panel.Branch, Is.EqualTo("release"));
        Assert.That(panel.IsLoading, Is.False);
    }

    [Test]
    public async Task LoadAsync_discardsAFailureFromASupersededWorkspace()
    {
        var client = new FakeGitApiProvider { HoldChanges = true };
        var panel = CreatePanel(client);
        var stale = panel.LoadAsync("ws-1");

        client.HoldChanges = false;
        client.Tree = OtherWorkspaceTree();
        await panel.LoadAsync("ws-2");

        client.FailHeldChanges("Connection refused");
        await stale;

        Assert.That(panel.ErrorMessage, Is.Null, "a superseded workspace must not raise an error for the current one");
        Assert.That(panel.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "Changes", "other.cs" }));
    }

    [Test]
    public async Task DeselectingTheWorkspaceMidLoadDoesNotLeaveThePanelLoading()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree(), HoldChanges = true };
        var panel = CreatePanel(client);
        var pending = panel.LoadAsync("ws-1");

        await panel.LoadAsync(null);

        client.CompleteHeldChanges(SampleTree());
        await pending;

        Assert.That(panel.IsLoading, Is.False);
        Assert.That(panel.Nodes, Is.Empty);
        Assert.That(panel.ShowEmptyState, Is.True);
    }

    [Test]
    public async Task OpeningAFileDiscardsADiffThatArrivesAfterAnotherFileWasOpened()
    {
        var client = new FakeGitApiProvider { Tree = SampleTree() };
        var panel = CreatePanel(client);
        await panel.LoadAsync("ws-1");

        client.HoldDiff = true;
        var stale = panel.Nodes[3].OpenCommand.Execute().FirstAsync().ToTask();

        client.HoldDiff = false;
        client.FileDiff = new GitFileDiffDto("src/app/util.cs", "Added", false, "+util");
        await panel.Nodes[4].OpenCommand.Execute().FirstAsync();

        client.CompleteHeldDiff(new GitFileDiffDto("src/app/main.cs", "Modified", false, "+main"));
        await stale;

        Assert.That(panel.Diff.Path, Is.EqualTo("src/app/util.cs"));
        Assert.That(panel.Diff.Content, Is.EqualTo("+util"));
    }

    private static GitChangeTreeDto OtherWorkspaceTree()
        => new(
            "ws-2",
            "release",
            1,
            new GitChangeDirectoryDto(
                string.Empty,
                string.Empty,
                [],
                [new GitChangeFileDto("other.cs", "other.cs", "Modified")]));

    private static GitPanelViewModel CreatePanel(IGitApiProvider client)
        => new(new GitController(new GitChangeListService(client)));

    private static GitChangeTreeDto SampleTree()
        => new(
            "ws-1",
            "main",
            3,
            new GitChangeDirectoryDto(
                string.Empty,
                string.Empty,
                [
                    new GitChangeDirectoryDto(
                        "src",
                        "src",
                        [
                            new GitChangeDirectoryDto("app", "src/app", [], [
                                new GitChangeFileDto("main.cs", "src/app/main.cs", "Modified"),
                                new GitChangeFileDto("util.cs", "src/app/util.cs", "Added")
                            ])
                        ],
                        [])
                ],
                [new GitChangeFileDto("README.md", "README.md", "Modified")]));
}

internal sealed class FakeGitApiProvider : IGitApiProvider
{
    private TaskCompletionSource<GitChangeTreeDto?>? _heldChanges;
    private TaskCompletionSource<GitFileDiffDto?>? _heldDiff;

    public GitChangeTreeDto? Tree { get; set; }

    public GitFileDiffDto? FileDiff { get; set; }

    public string? ChangesFailure { get; set; }

    // When set, the next call parks until the test completes it, so a response can be made to
    // arrive after a newer request has already been issued.
    public bool HoldChanges { get; set; }

    public bool HoldDiff { get; set; }

    public void CompleteHeldChanges(GitChangeTreeDto? tree) => _heldChanges!.SetResult(tree);

    public void CompleteHeldDiff(GitFileDiffDto? diff) => _heldDiff!.SetResult(diff);

    public void FailHeldChanges(string message) => _heldChanges!.SetException(new HttpRequestException(message));

    public GitCommitResultDto CommitResult { get; set; } = new(true, true, "0123456789abcdef", null);

    public GitCommitRequestDto? CommittedRequest { get; private set; }

    public GitFilesRequestDto? DiscardedRequest { get; private set; }

    public GitBranchRequestDto? BranchRequest { get; private set; }

    public GitMutationResultDto MutationResult { get; set; } = new(true, true, null);

    public int ChangeRequests { get; private set; }

    public Task<GitChangeTreeDto?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        ChangeRequests++;
        if (ChangesFailure is not null)
            return Task.FromException<GitChangeTreeDto?>(new HttpRequestException(ChangesFailure));

        if (!HoldChanges)
            return Task.FromResult(Tree);

        _heldChanges = new TaskCompletionSource<GitChangeTreeDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _heldChanges.Task;
    }

    public Task<GitFileDiffDto?> GetFileDiffAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
    {
        if (!HoldDiff)
            return Task.FromResult(FileDiff);

        _heldDiff = new TaskCompletionSource<GitFileDiffDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _heldDiff.Task;
    }

    public Task<GitCommitResultDto> CommitAsync(string workspaceId, GitCommitRequestDto request, CancellationToken cancellationToken = default)
    {
        CommittedRequest = request;
        return Task.FromResult(CommitResult);
    }

    public Task<GitMutationResultDto> DiscardAsync(string workspaceId, GitFilesRequestDto request, CancellationToken cancellationToken = default)
    {
        DiscardedRequest = request;
        return Task.FromResult(MutationResult);
    }

    public Task<GitMutationResultDto> SwitchBranchAsync(string workspaceId, GitBranchRequestDto request, CancellationToken cancellationToken = default)
    {
        BranchRequest = request;
        return Task.FromResult(MutationResult);
    }
}
