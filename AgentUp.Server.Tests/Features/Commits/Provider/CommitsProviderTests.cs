using AgentUp.Server.Features.Commits.Interfaces;
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

    private async Task<string> CreateRepositoryAsync()
    {
        _tempRoot = Path.Join(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        await RunGitAsync(_tempRoot, "init");
        return _tempRoot;
    }

    private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
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
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(" ", arguments)} failed: {stderr.Trim()}");
    }
}
