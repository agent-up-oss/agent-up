using System.Diagnostics;
using AgentUp.CLI.Features.Commits.Providers;

namespace AgentUp.CLI.Tests.Features.Commits.Provider;

[TestFixture]
public sealed class CommitsGitProviderTests
{
    private string _repoRoot = null!;

    [SetUp]
    public async Task SetUp()
    {
        _repoRoot = Path.Join(Path.GetTempPath(), "AgentUp-CommitsGitProviderTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_repoRoot);
        await RunGitAsync("init");
        await File.WriteAllTextAsync(Path.Join(_repoRoot, "README.md"), "test");
        await RunGitAsync("add", "README.md");
        await RunGitAsync("-c", "user.name=Agent Up", "-c", "user.email=agent-up@example.test", "commit", "-m", "test: initialize");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
            Directory.Delete(_repoRoot, recursive: true);
    }

    [Test]
    public void GetDiffAsync_rejectsPathsThatEscapeRepositoryRoot()
    {
        var provider = new CommitsGitProvider(_repoRoot);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetDiffAsync(["../outside.txt"]));

        Assert.That(ex!.Message, Does.Contain("must stay under the repository root"));
    }

    [Test]
    public void StageFilesAsync_rejectsGitPathspecMagic()
    {
        var provider = new CommitsGitProvider(_repoRoot);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.StageFilesAsync([":(glob)**"]));

        Assert.That(ex!.Message, Does.Contain("literal path under the repository root"));
    }

    [Test]
    public async Task GetDiffAsync_includesBinaryPatchForUntrackedBinaryFile()
    {
        await File.WriteAllBytesAsync(Path.Join(_repoRoot, "wrapper.jar"), [0, 1, 2, 3, 255]);
        var provider = new CommitsGitProvider(_repoRoot);

        var patch = await provider.GetDiffAsync(["wrapper.jar"]);

        Assert.That(patch, Does.Contain("GIT binary patch"));
        Assert.That(patch, Does.Contain("wrapper.jar"));
    }

    [Test]
    public async Task GetDiffAsync_binaryPatchCanBeApplied()
    {
        var expected = new byte[] { 0, 1, 2, 3, 255 };
        await File.WriteAllBytesAsync(Path.Join(_repoRoot, "wrapper.jar"), expected);
        var provider = new CommitsGitProvider(_repoRoot);
        var patch = await provider.GetDiffAsync(["wrapper.jar"]);
        File.Delete(Path.Join(_repoRoot, "wrapper.jar"));

        await provider.ApplyPatchAsync(patch);

        Assert.That(await File.ReadAllBytesAsync(Path.Join(_repoRoot, "wrapper.jar")), Is.EqualTo(expected));
    }

    private async Task RunGitAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Failed to start git process.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git failed while preparing test repository.");
    }
}
