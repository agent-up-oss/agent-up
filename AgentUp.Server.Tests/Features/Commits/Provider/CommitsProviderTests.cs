using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Providers;

namespace AgentUp.Server.Tests.Features.Commits.Provider;

[TestFixture]
public sealed class CommitsProviderTests
{
    private string? _tempRoot;

    [TearDown]
    public void TearDown()
    {
        if (_tempRoot is not null && Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Test]
    public void CommitsGitProvider_implementsInterface()
    {
        var provider = new CommitsGitProvider();

        Assert.That(provider, Is.InstanceOf<ICommitsGitProvider>());
    }

    [Test]
    public void CommitsQueueProvider_implementsInterface()
    {
        var provider = new CommitsQueueProvider(new CommitsGitProvider());

        Assert.That(provider, Is.InstanceOf<ICommitsQueueProvider>());
    }

    [Test]
    public void GetRepoRootAsync_rejectsMalformedWorktreePathBeforeLaunchingGit()
    {
        var provider = new CommitsGitProvider();
        var path = Path.Join(TestContext.CurrentContext.WorkDirectory, "repo") + "\n--upload-pack=malicious";

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetRepoRootAsync(path));

        Assert.That(exception!.Message, Does.Contain("worktree path"));
    }

    [Test]
    public async Task GetDiffAsync_rejectsGitPathspecMagic()
    {
        var repositoryPath = await CreateRepositoryAsync();
        var provider = new CommitsGitProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetDiffAsync(repositoryPath, [":(glob)**/*.cs"]));

        Assert.That(exception!.Message, Does.Contain("literal path"));
    }

    [Test]
    public async Task GetDiffAsync_rejectsPathsOutsideRepository()
    {
        var repositoryPath = await CreateRepositoryAsync();
        var provider = new CommitsGitProvider();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.GetDiffAsync(repositoryPath, ["../outside.txt"]));

        Assert.That(exception!.Message, Does.Contain("repository root"));
    }

    [Test]
    public async Task GetDiffAsync_allowsRepositoryFileNamesBeginningWithDots()
    {
        var repositoryPath = await CreateRepositoryAsync();
        await RunGitAsync(repositoryPath, "-c", "user.name=Agent Up", "-c", "user.email=agent-up@example.invalid", "commit", "--allow-empty", "-m", "initial");
        var filePath = Path.Join(repositoryPath, "..notes");
        await File.WriteAllTextAsync(filePath, "notes");
        var provider = new CommitsGitProvider();

        var diff = await provider.GetDiffAsync(repositoryPath, ["..notes"]);

        Assert.That(diff, Does.Contain("..notes"));
    }

    [Test]
    public async Task GetModifiedFilesAsync_parsesNulDelimitedSpecialPaths()
    {
        var repositoryPath = await CreateRepositoryAsync();
        await File.WriteAllTextAsync(Path.Join(repositoryPath, "name with space.cs"), "content");
        var provider = new CommitsGitProvider();

        var files = await provider.GetModifiedFilesAsync(repositoryPath);

        Assert.That(files, Is.EqualTo(new[] { "name with space.cs" }));
    }

    [Test]
    public async Task GetModifiedFilesAsync_reportsRenameDestination()
    {
        var repositoryPath = await CreateRepositoryAsync();
        await File.WriteAllTextAsync(Path.Join(repositoryPath, "old name.cs"), "content");
        await RunGitAsync(repositoryPath, "add", "old name.cs");
        await RunGitAsync(repositoryPath, "-c", "user.name=Agent Up", "-c", "user.email=agent-up@example.invalid", "commit", "-m", "initial");
        await RunGitAsync(repositoryPath, "mv", "old name.cs", "new name.cs");
        var provider = new CommitsGitProvider();

        var files = await provider.GetModifiedFilesAsync(repositoryPath);

        Assert.That(files, Is.EqualTo(new[] { "new name.cs" }));
    }

    [Test]
    public async Task GetOperationStateAsync_reportsActiveMerge()
    {
        var repositoryPath = await CreateRepositoryAsync();
        await File.WriteAllTextAsync(Path.Join(repositoryPath, "file.txt"), "base\n");
        await RunGitAsync(repositoryPath, "add", "file.txt");
        await RunGitAsync(repositoryPath, "-c", "user.name=Agent Up", "-c", "user.email=agent-up@example.invalid", "commit", "-m", "initial");
        await RunGitAsync(repositoryPath, "branch", "-M", "main");
        await RunGitAsync(repositoryPath, "checkout", "-b", "feature");
        await File.WriteAllTextAsync(Path.Join(repositoryPath, "file.txt"), "feature\n");
        await RunGitAsync(repositoryPath, "-c", "user.name=Agent Up", "-c", "user.email=agent-up@example.invalid", "commit", "-am", "feature");
        await RunGitAsync(repositoryPath, "checkout", "main");
        await File.WriteAllTextAsync(Path.Join(repositoryPath, "file.txt"), "main\n");
        await RunGitAsync(repositoryPath, "-c", "user.name=Agent Up", "-c", "user.email=agent-up@example.invalid", "commit", "-am", "main");

        await RunGitAsync(repositoryPath, ["merge", "feature"], [1]);

        var state = await new CommitsGitProvider().GetOperationStateAsync(repositoryPath);

        Assert.That(state.Blocking, Is.True);
        Assert.That(state.Kind, Is.EqualTo("merge"));
    }

    [Test]
    public async Task ApplyPatchAsync_killsGitApplyAndPropagatesCancellation()
    {
        var repositoryPath = await CreateRepositoryAsync();
        var provider = new CommitsGitProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.That(
            Assert.CatchAsync(async () => await provider.ApplyPatchAsync(
                repositoryPath,
                "diff --git a/a.cs b/a.cs\n",
                cancellation.Token)),
            Is.AssignableTo<OperationCanceledException>());
    }

    [Test]
    public async Task CommitsQueueProvider_persistsQueueAndPatches()
    {
        var repositoryPath = await CreateRepositoryAsync();
        var provider = new CommitsQueueProvider(new FixedRootGitProvider(repositoryPath), _tempRoot);
        var queue = new CommitsQueue(1, [new CommitEntry("Slice", "feat: thing", ["a.cs"], ["dotnet test"], "entry-1", "patch-1")]);

        await provider.WriteAsync(repositoryPath, queue);
        await provider.SavePatchAsync(repositoryPath, "patch-1", "diff --git a/a.cs b/a.cs\n");

        var read = await provider.ReadAsync(repositoryPath);
        var patch = await provider.ReadPatchAsync(repositoryPath, "patch-1");

        Assert.That(read.Commits, Has.Count.EqualTo(1));
        Assert.That(read.Commits[0].Slice, Is.EqualTo("Slice"));
        Assert.That(patch, Does.Contain("diff --git"));
    }

    [Test]
    public async Task CommitsQueueProvider_deletePatchRemovesPersistedPatch()
    {
        var repositoryPath = await CreateRepositoryAsync();
        var provider = new CommitsQueueProvider(new FixedRootGitProvider(repositoryPath), _tempRoot);

        await provider.SavePatchAsync(repositoryPath, "patch-1", "diff --git a/a.cs b/a.cs\n");
        await provider.DeletePatchAsync(repositoryPath, "patch-1");

        Assert.That(await provider.ReadPatchAsync(repositoryPath, "patch-1"), Is.Null);
    }

    [Test]
    public async Task CommitsQueueProvider_preservesRepositoryPathCaseInQueueIdentity()
    {
        var upperRoot = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"), "App");
        var lowerRoot = Path.Join(Path.GetDirectoryName(upperRoot)!, "app");
        Directory.CreateDirectory(upperRoot);
        Directory.CreateDirectory(lowerRoot);
        _tempRoot = Path.GetDirectoryName(upperRoot);
        var provider = new CommitsQueueProvider(new MappingRootGitProvider(new Dictionary<string, string>
        {
            [upperRoot] = upperRoot,
            [lowerRoot] = lowerRoot
        }), _tempRoot);

        await provider.WriteAsync(upperRoot, new CommitsQueue(1, [new CommitEntry("Upper", "m", ["a.cs"], [])]));
        await provider.WriteAsync(lowerRoot, new CommitsQueue(1, [new CommitEntry("Lower", "m", ["b.cs"], [])]));

        Assert.That((await provider.ReadAsync(upperRoot)).Commits[0].Slice, Is.EqualTo("Upper"));
        Assert.That((await provider.ReadAsync(lowerRoot)).Commits[0].Slice, Is.EqualTo("Lower"));
    }

    private async Task<string> CreateRepositoryAsync()
    {
        _tempRoot = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        await RunGitAsync(_tempRoot, "init");
        return _tempRoot;
    }

    private static Task RunGitAsync(string workingDirectory, params string[] arguments)
        => RunGitAsync(workingDirectory, arguments, [0]);

    private static async Task RunGitAsync(string workingDirectory, string[] arguments, int[] allowedExitCodes)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!allowedExitCodes.Contains(process.ExitCode))
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {stderr.Trim()}");
    }

    private sealed class FixedRootGitProvider(string root) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(root);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class MappingRootGitProvider(IReadOnlyDictionary<string, string> roots) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(roots[worktreePath]);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
