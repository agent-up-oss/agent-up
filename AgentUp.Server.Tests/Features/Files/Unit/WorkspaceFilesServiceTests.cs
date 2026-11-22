using System.Text;
using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Files.Models;
using AgentUp.Server.Features.Files.Providers;
using AgentUp.Server.Features.Files.Services;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Files.Unit;

[TestFixture]
public sealed class WorkspaceFilesServiceTests
{
    private static readonly WorkspaceFileTextProvider Text = new();

    [Test]
    public async Task GetDirectory_reportsUnknownWorkspaces()
    {
        var (service, _) = await ComposeAsync(new FakeWorkspaceFileSystemProvider(), new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var result = await service.GetDirectoryAsync("missing", "");

        Assert.That(result.Outcome, Is.EqualTo(WorkspaceFileOutcome.WorkspaceNotFound));
    }

    [Test]
    public async Task GetDirectory_readsTheWorkspaceFolderItself()
    {
        var files = Disk(("README.md", "# widgets\n"));
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        await service.GetDirectoryAsync(workspaceId, "");

        Assert.That(files.LastRoot, Is.EqualTo(ServerDomain.WorktreePath));
    }

    [Test]
    public async Task GetDirectory_marksChangedFilesWithTheirGitStatus()
    {
        var changes = Changes(("README.md", GitChangeStatus.Modified));
        var (service, workspaceId) = await ComposeAsync(Disk(("README.md", "# widgets\n"), ("LICENSE", "MIT\n")), changes, new FakeWorkspaceFileEditGuard());

        var listing = (await service.GetDirectoryAsync(workspaceId, "")).Value!;

        Assert.That(listing.Entries.Single(entry => entry.Name == "README.md").Status, Is.EqualTo(GitChangeStatus.Modified));
        Assert.That(listing.Entries.Single(entry => entry.Name == "LICENSE").Status, Is.Null);
    }

    [Test]
    public async Task GetDirectory_countsChangedFilesUnderAFolderButNotDeletedOnes()
    {
        var changes = Changes(
            ("src/app/main.cs", GitChangeStatus.Modified),
            ("src/new.cs", GitChangeStatus.Untracked),
            ("src/gone.cs", GitChangeStatus.Deleted),
            ("srcx/other.cs", GitChangeStatus.Modified));
        var (service, workspaceId) = await ComposeAsync(Disk(("src/app/main.cs", "x"), ("src/new.cs", "y")), changes, new FakeWorkspaceFileEditGuard());

        var listing = (await service.GetDirectoryAsync(workspaceId, "")).Value!;

        var src = listing.Entries.Single(entry => entry.Name == "src");
        Assert.That(src.Kind, Is.EqualTo(WorkspaceFileEntryKind.Directory));
        Assert.That(src.ChangedFiles, Is.EqualTo(2));
    }

    [Test]
    public async Task GetDirectory_answersInvalidForAPathOutsideTheFolder()
    {
        var (service, workspaceId) = await ComposeAsync(Disk(), new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var result = await service.GetDirectoryAsync(workspaceId, "../etc");

        Assert.That(result.Outcome, Is.EqualTo(WorkspaceFileOutcome.Invalid));
    }

    [Test]
    public async Task GetDirectory_answersNotFoundForAMissingFolder()
    {
        var (service, workspaceId) = await ComposeAsync(Disk(("README.md", "x")), new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var result = await service.GetDirectoryAsync(workspaceId, "nowhere");

        Assert.That(result.Outcome, Is.EqualTo(WorkspaceFileOutcome.FileNotFound));
    }

    [Test]
    public async Task GetFile_returnsTheWholeTextWithItsVersionAndStatus()
    {
        var changes = Changes(("src/main.cs", GitChangeStatus.Added));
        var (service, workspaceId) = await ComposeAsync(Disk(("src/main.cs", "line one\nline two\n")), changes, new FakeWorkspaceFileEditGuard());

        var file = (await service.GetFileAsync(workspaceId, "src/main.cs")).Value!;

        Assert.That(file.Content, Is.EqualTo("line one\nline two\n"));
        Assert.That(file.Version, Is.EqualTo(Text.Version(Encoding.UTF8.GetBytes("line one\nline two\n"))));
        Assert.That(file.Status, Is.EqualTo(GitChangeStatus.Added));
    }

    [Test]
    public async Task GetFile_describesABinaryFileWithoutContent()
    {
        var files = new FakeWorkspaceFileSystemProvider();
        files.Files["logo.png"] = [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01];
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var file = (await service.GetFileAsync(workspaceId, "logo.png")).Value!;

        Assert.That(file.IsBinary, Is.True);
        Assert.That(file.Content, Is.Null);
    }

    [Test]
    public async Task GetFile_describesAFileOverTheLimitWithoutContent()
    {
        var files = new FakeWorkspaceFileSystemProvider();
        files.Files["dump.log"] = new byte[WorkspaceFileLimits.MaxContentBytes + 1];
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var file = (await service.GetFileAsync(workspaceId, "dump.log")).Value!;

        Assert.That(file.IsTooLarge, Is.True);
        Assert.That(file.Content, Is.Null);
    }

    [Test]
    public async Task WriteFile_replacesTheFileWhenTheVersionMatches()
    {
        var files = Disk(("README.md", "old\n"));
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());
        var opened = (await service.GetFileAsync(workspaceId, "README.md")).Value!;

        var saved = await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("README.md", "new\r\n", opened.Version));

        Assert.That(saved.Outcome, Is.EqualTo(WorkspaceFileOutcome.Succeeded));
        Assert.That(Encoding.UTF8.GetString(files.Files["README.md"]), Is.EqualTo("new\r\n"));
        Assert.That(saved.Value!.Version, Is.Not.EqualTo(opened.Version));
    }

    [Test]
    public async Task WriteFile_keepsAByteOrderMark()
    {
        var files = new FakeWorkspaceFileSystemProvider();
        files.Files["notes.txt"] = [0xEF, 0xBB, 0xBF, (byte)'a'];
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());
        var opened = (await service.GetFileAsync(workspaceId, "notes.txt")).Value!;

        await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("notes.txt", "b", opened.Version));

        Assert.That(opened.Content, Is.EqualTo("a"));
        Assert.That(files.Files["notes.txt"], Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'b' }));
    }

    [Test]
    public async Task WriteFile_refusesAStaleVersionAndLeavesTheFile()
    {
        var files = Disk(("README.md", "agent's edit\n"));
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var saved = await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("README.md", "mine\n", Text.Version(Encoding.UTF8.GetBytes("old\n"))));

        Assert.That(saved.Outcome, Is.EqualTo(WorkspaceFileOutcome.Stale));
        Assert.That(files.Writes, Is.Zero);
    }

    [Test]
    public async Task WriteFile_refusesWhileTheAgentIsWorking()
    {
        var files = Disk(("README.md", "old\n"));
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard { Working = true });

        var saved = await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("README.md", "new\n", Text.Version(files.Files["README.md"])));

        Assert.That(saved.Outcome, Is.EqualTo(WorkspaceFileOutcome.Busy));
        Assert.That(files.Writes, Is.Zero);
    }

    [Test]
    public async Task WriteFile_refusesABinaryFile()
    {
        var files = new FakeWorkspaceFileSystemProvider();
        files.Files["logo.png"] = [0x00, 0x01];
        var (service, workspaceId) = await ComposeAsync(files, new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var saved = await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("logo.png", "text", Text.Version(files.Files["logo.png"])));

        Assert.That(saved.Outcome, Is.EqualTo(WorkspaceFileOutcome.Invalid));
    }

    [Test]
    public async Task WriteFile_requiresContentAndVersion()
    {
        var (service, workspaceId) = await ComposeAsync(Disk(("README.md", "old\n")), new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var saved = await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("README.md", null, null));

        Assert.That(saved.Outcome, Is.EqualTo(WorkspaceFileOutcome.Invalid));
    }

    [Test]
    public async Task WriteFile_answersNotFoundForAMissingFile()
    {
        var (service, workspaceId) = await ComposeAsync(Disk(), new FakeWorkspaceFileChangeProvider(), new FakeWorkspaceFileEditGuard());

        var saved = await service.WriteFileAsync(workspaceId, new WorkspaceFileWriteRequest("missing.md", "x", "v1"));

        Assert.That(saved.Outcome, Is.EqualTo(WorkspaceFileOutcome.FileNotFound));
    }

    private static FakeWorkspaceFileSystemProvider Disk(params (string Path, string Text)[] files)
    {
        var disk = new FakeWorkspaceFileSystemProvider();
        foreach (var (path, text) in files)
            disk.Files[path] = Encoding.UTF8.GetBytes(text);
        return disk;
    }

    private static FakeWorkspaceFileChangeProvider Changes(params (string Path, GitChangeStatus Status)[] entries)
    {
        var changes = new FakeWorkspaceFileChangeProvider();
        changes.Changes.AddRange(entries.Select(entry => new GitChangeEntry(entry.Path, entry.Status)));
        return changes;
    }

    private static async Task<(WorkspaceFilesService Service, string WorkspaceId)> ComposeAsync(
        FakeWorkspaceFileSystemProvider files,
        FakeWorkspaceFileChangeProvider changes,
        FakeWorkspaceFileEditGuard guard)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        return (WorkspaceFilesComposition.Service(workspaces, files, changes, guard), workspace.Id);
    }
}
