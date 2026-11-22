using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Files.Models;
using AgentUp.Server.Features.Files.Providers;

namespace AgentUp.Server.Tests.Features.Files.Provider;

[TestFixture]
public sealed class WorkspaceFileSystemProviderTests
{
    private readonly List<string> _folders = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var folder in _folders.Where(Directory.Exists))
            Directory.Delete(folder, recursive: true);
    }

    [Test]
    public void ListDirectory_returnsOneLevelWithFoldersFirst()
    {
        var root = Workspace(("src/app/main.cs", "x"), ("README.md", "readme"), ("assets/logo.svg", "<svg/>"));

        var listing = new WorkspaceFileSystemProvider().ListDirectory(root, "", 100)!;

        Assert.That(listing.Entries.Select(entry => entry.Name), Is.EqualTo(new[] { "assets", "src", "README.md" }));
        Assert.That(listing.Entries[2].Size, Is.EqualTo(6));
        Assert.That(listing.Entries[1].Kind, Is.EqualTo(WorkspaceFileEntryKind.Directory));
    }

    [Test]
    public void ListDirectory_namesNestedEntriesByTheirWorkspacePath()
    {
        var root = Workspace(("src/app/main.cs", "x"));

        var listing = new WorkspaceFileSystemProvider().ListDirectory(root, "src/./app/", 100)!;

        Assert.That(listing.Path, Is.EqualTo("src/app"));
        Assert.That(listing.Entries.Single().Path, Is.EqualTo("src/app/main.cs"));
    }

    [Test]
    public void ListDirectory_leavesOutTheGitFolder()
    {
        var root = Workspace((".git/HEAD", "ref: refs/heads/main"), (".gitignore", "bin/"));

        var listing = new WorkspaceFileSystemProvider().ListDirectory(root, "", 100)!;

        Assert.That(listing.Entries.Select(entry => entry.Name), Is.EqualTo(new[] { ".gitignore" }));
    }

    [Test]
    public void ListDirectory_cutsALargeFolderAndSaysSo()
    {
        var root = Workspace(("a.txt", "a"), ("b.txt", "b"), ("c.txt", "c"));

        var listing = new WorkspaceFileSystemProvider().ListDirectory(root, "", 2)!;

        Assert.That(listing.Entries, Has.Count.EqualTo(2));
        Assert.That(listing.Truncated, Is.True);
    }

    [Test]
    public void ListDirectory_isNullForAFile()
    {
        var root = Workspace(("README.md", "x"));

        Assert.That(new WorkspaceFileSystemProvider().ListDirectory(root, "README.md", 100), Is.Null);
    }

    [TestCase("../outside")]
    [TestCase("src/../../outside")]
    [TestCase("/etc")]
    [TestCase(".git")]
    [TestCase("src/.GIT/config")]
    [TestCase("src\\main.cs")]
    public void ListDirectory_refusesPathsThatLeaveTheWorkspace(string path)
    {
        var root = Workspace(("src/main.cs", "x"));

        Assert.That(() => new WorkspaceFileSystemProvider().ListDirectory(root, path, 100), Throws.TypeOf<WorkspaceFilePathException>());
    }

    [Test]
    public void ListDirectory_leavesOutALinkThatLeadsOutside()
    {
        var root = Workspace(("README.md", "x"));
        var outside = Workspace(("secret.txt", "secret"));
        File.CreateSymbolicLink(Path.Join(root, "escape"), outside);
        File.CreateSymbolicLink(Path.Join(root, "inside"), Path.Join(root, "README.md"));

        var listing = new WorkspaceFileSystemProvider().ListDirectory(root, "", 100)!;

        Assert.That(listing.Entries.Select(entry => entry.Name), Is.EqualTo(new[] { "inside", "README.md" }));
    }

    [Test]
    public void ReadFile_refusesAPathThroughALinkThatLeadsOutside()
    {
        var root = Workspace(("README.md", "x"));
        var outside = Workspace(("secret.txt", "secret"));
        File.CreateSymbolicLink(Path.Join(root, "escape"), outside);

        Assert.That(
            async () => await new WorkspaceFileSystemProvider().ReadFileAsync(root, "escape/secret.txt", 1024, CancellationToken.None),
            Throws.TypeOf<WorkspaceFilePathException>());
    }

    [Test]
    public void ListDirectory_leavesOutALinkThatLoopsAndKeepsTheRest()
    {
        var root = Workspace(("README.md", "x"));
        File.CreateSymbolicLink(Path.Join(root, "loop-a"), Path.Join(root, "loop-b"));
        File.CreateSymbolicLink(Path.Join(root, "loop-b"), Path.Join(root, "loop-a"));

        var listing = new WorkspaceFileSystemProvider().ListDirectory(root, "", 100)!;

        Assert.That(listing.Entries.Select(entry => entry.Name), Is.EqualTo(new[] { "README.md" }));
    }

    [Test]
    public void ReadFile_throwsIOForALinkThatLoops()
    {
        var root = Workspace(("README.md", "x"));
        File.CreateSymbolicLink(Path.Join(root, "loop-a"), Path.Join(root, "loop-b"));
        File.CreateSymbolicLink(Path.Join(root, "loop-b"), Path.Join(root, "loop-a"));

        Assert.That(
            async () => await new WorkspaceFileSystemProvider().ReadFileAsync(root, "loop-a", 1024, CancellationToken.None),
            Throws.InstanceOf<IOException>());
    }

    [Test]
    public async Task ReadFile_isNullUnderAFolderThatDoesNotExist()
        => Assert.That(await new WorkspaceFileSystemProvider().ReadFileAsync(Workspace(("README.md", "x")), "nowhere/at/all.md", 1024, CancellationToken.None), Is.Null);

    [Test]
    public async Task ReadFile_isNullThroughABrokenLink()
    {
        var root = Workspace(("README.md", "x"));
        File.CreateSymbolicLink(Path.Join(root, "broken"), Path.Join(root, "gone"));

        Assert.That(await new WorkspaceFileSystemProvider().ReadFileAsync(root, "broken/file.md", 1024, CancellationToken.None), Is.Null);
    }

    [Test]
    public void ReadFile_needsAFileName()
        => Assert.That(
            async () => await new WorkspaceFileSystemProvider().ReadFileAsync(Workspace(("README.md", "x")), "", 1024, CancellationToken.None),
            Throws.TypeOf<WorkspaceFilePathException>());

    [Test]
    public async Task ReadFile_returnsTheWholeFile()
    {
        var root = Workspace(("src/main.cs", "line one\nline two\n"));

        var file = await new WorkspaceFileSystemProvider().ReadFileAsync(root, "src/main.cs", 1024, CancellationToken.None);

        Assert.That(file!.Bytes, Has.Length.EqualTo(18));
        Assert.That(file.Path, Is.EqualTo("src/main.cs"));
    }

    [Test]
    public async Task ReadFile_returnsOnlyTheSizeOverTheLimit()
    {
        var root = Workspace(("big.log", "0123456789"));

        var file = await new WorkspaceFileSystemProvider().ReadFileAsync(root, "big.log", 4, CancellationToken.None);

        Assert.That(file!.Bytes, Is.Null);
        Assert.That(file.Size, Is.EqualTo(10));
    }

    [Test]
    public async Task ReadFile_isNullForAFolderOrAMissingFile()
    {
        var root = Workspace(("src/main.cs", "x"));
        var provider = new WorkspaceFileSystemProvider();

        Assert.That(await provider.ReadFileAsync(root, "src", 1024, CancellationToken.None), Is.Null);
        Assert.That(await provider.ReadFileAsync(root, "missing.md", 1024, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task WriteFile_replacesTheWholeFile()
    {
        var root = Workspace(("README.md", "a much longer original\n"));

        await new WorkspaceFileSystemProvider().WriteFileAsync(root, "README.md", "short\n"u8.ToArray(), CancellationToken.None);

        Assert.That(await File.ReadAllTextAsync(Path.Join(root, "README.md")), Is.EqualTo("short\n"));
    }

    [Test]
    public void WriteFile_neverCreatesAFile()
    {
        var root = Workspace(("README.md", "x"));

        Assert.That(
            async () => await new WorkspaceFileSystemProvider().WriteFileAsync(root, "new.md", "x"u8.ToArray(), CancellationToken.None),
            Throws.TypeOf<FileNotFoundException>());
        Assert.That(File.Exists(Path.Join(root, "new.md")), Is.False);
    }

    private string Workspace(params (string Path, string Text)[] files)
    {
        var root = Directory.CreateTempSubdirectory("agent-up-files").FullName;
        _folders.Add(root);
        foreach (var (path, text) in files)
        {
            var full = Path.Join(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        return root;
    }
}
