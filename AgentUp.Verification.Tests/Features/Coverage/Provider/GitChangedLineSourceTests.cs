using System.Diagnostics;
using AgentUp.Verification.Features.Coverage.Providers;

namespace AgentUp.Verification.Tests.Features.Coverage.Provider;

[TestFixture]
public sealed class GitChangedLineSourceTests
{
    private static string CreateRepository()
    {
        var root = Path.Join(
            TestContext.CurrentContext.WorkDirectory, "git-lines-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Git(root, "init", "--initial-branch=main");
        Git(root, "config", "user.email", "test@agent-up.local");
        Git(root, "config", "user.name", "Agent-Up Test");
        return root;
    }

    private static void Git(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo);
        process!.WaitForExit();
    }

    private static void Write(string root, string relativePath, params string[] lines)
    {
        var absolute = Path.Join(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllLines(absolute, lines);
    }

    private static GitChangedLineSource Source() => new(new UnifiedDiffParser());

    [Test]
    public async Task GetChangedLinesAsync_reportsEveryLineOfAnUntrackedFile()
    {
        // A brand-new file has no diff. Without this it would contribute nothing and read
        // as fully covered.
        var root = CreateRepository();
        Write(root, "a.txt", "one", "two", "three");
        Git(root, "add", "a.txt");
        Git(root, "commit", "-m", "base");
        Write(root, "new.cs", "line1", "line2");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines["new.cs"], Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task GetChangedLinesAsync_reportsOnlyTheModifiedLineOfATrackedFile()
    {
        var root = CreateRepository();
        Write(root, "a.cs", "one", "two", "three", "four");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "base");
        Write(root, "a.cs", "one", "CHANGED", "three", "four");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines["a.cs"], Is.EquivalentTo(new[] { 2 }));
    }

    [Test]
    public async Task GetChangedLinesAsync_reportsAddedLinesAtTheirNewPositions()
    {
        var root = CreateRepository();
        Write(root, "a.cs", "one", "four");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "base");
        Write(root, "a.cs", "one", "two", "three", "four");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines["a.cs"], Is.EquivalentTo(new[] { 2, 3 }));
    }

    [Test]
    public async Task GetChangedLinesAsync_ignoresAPureDeletion()
    {
        var root = CreateRepository();
        Write(root, "a.cs", "one", "two", "three");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "base");
        Write(root, "a.cs", "one", "three");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines.TryGetValue("a.cs", out _), Is.False,
            "Removing a line requires no coverage.");
    }

    [Test]
    public async Task GetChangedLinesAsync_includesLinesCommittedOnTheBranchAboveItsBase()
    {
        var root = CreateRepository();
        Write(root, "a.cs", "one", "two");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "base");
        Git(root, "checkout", "-b", "feature");
        Write(root, "a.cs", "one", "TWO-CHANGED");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "feature work");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines["a.cs"], Is.EquivalentTo(new[] { 2 }),
            "Work already committed on the branch still needs covering.");
    }

    [Test]
    public async Task GetChangedLinesAsync_respectsGitignoreForUntrackedFiles()
    {
        var root = CreateRepository();
        Write(root, ".gitignore", "ignored/");
        Git(root, "add", ".gitignore");
        Git(root, "commit", "-m", "base");
        Write(root, "ignored/generated.cs", "noise");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines.Keys, Does.Not.Contain("ignored/generated.cs"));
    }

    [Test]
    public async Task GetChangedLinesAsync_reportsNothingForACleanCheckout()
    {
        var root = CreateRepository();
        Write(root, "a.cs", "one");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "base");

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines, Is.Empty);
    }

    [Test]
    public async Task GetChangedLinesAsync_skipsAnEmptyUntrackedFile()
    {
        var root = CreateRepository();
        Write(root, "a.cs", "one");
        Git(root, "add", "a.cs");
        Git(root, "commit", "-m", "base");
        File.WriteAllText(Path.Join(root, "empty.cs"), string.Empty);

        var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

        Assert.That(changed.Lines.Keys, Does.Not.Contain("empty.cs"));
    }

    [Test]
    public async Task GetChangedLinesAsync_reportsNothingOutsideAGitCheckout()
    {
        // Deliberately under the system temp path rather than the test work directory:
        // the latter sits inside this repository, and Git walks up to the enclosing
        // checkout, so a directory there is not outside a repository at all.
        var root = Path.Join(Path.GetTempPath(), "agent-up-no-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var changed = await Source().GetChangedLinesAsync(root, CancellationToken.None);

            Assert.That(changed.Lines, Is.Empty, "Git exits non-zero, which must yield no changed lines.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
