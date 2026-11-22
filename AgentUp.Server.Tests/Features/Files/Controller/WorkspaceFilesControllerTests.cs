using System.Text;
using AgentUp.Server.Features.Files.Controllers;
using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.Files.Controller;

[TestFixture]
public sealed class WorkspaceFilesControllerTests
{
    [Test]
    public async Task GetDirectory_isNotFoundForAnUnknownWorkspace()
    {
        var (controller, _) = await ComposeAsync(new FakeWorkspaceFileSystemProvider(), new FakeWorkspaceFileEditGuard());

        Assert.That(StatusOf(await controller.GetDirectory("missing", null)), Is.EqualTo(404));
    }

    [Test]
    public async Task GetDirectory_returnsTheListing()
    {
        var (controller, workspaceId) = await ComposeAsync(Disk("README.md", "x"), new FakeWorkspaceFileEditGuard());

        var result = await controller.GetDirectory(workspaceId, null);

        var listing = (WorkspaceDirectoryListing)((OkObjectResult)result).Value!;
        Assert.That(listing.Entries.Single().Name, Is.EqualTo("README.md"));
    }

    [Test]
    public async Task GetFile_isABadRequestForAPathOutsideTheWorkspace()
    {
        var (controller, workspaceId) = await ComposeAsync(Disk("README.md", "x"), new FakeWorkspaceFileEditGuard());

        Assert.That(StatusOf(await controller.GetFile(workspaceId, "../secret")), Is.EqualTo(400));
    }

    [Test]
    public async Task GetFile_isNotFoundForAMissingFile()
    {
        var (controller, workspaceId) = await ComposeAsync(Disk("README.md", "x"), new FakeWorkspaceFileEditGuard());

        Assert.That(StatusOf(await controller.GetFile(workspaceId, "missing.md")), Is.EqualTo(404));
    }

    [Test]
    public async Task PutFile_isAConflictForAStaleVersion()
    {
        var (controller, workspaceId) = await ComposeAsync(Disk("README.md", "x"), new FakeWorkspaceFileEditGuard());

        var result = await controller.PutFile(workspaceId, new WorkspaceFileWriteRequest("README.md", "y", "stale"));

        Assert.That(StatusOf(result), Is.EqualTo(409));
    }

    [Test]
    public async Task PutFile_isAConflictWhileTheAgentIsWorking()
    {
        var (controller, workspaceId) = await ComposeAsync(Disk("README.md", "x"), new FakeWorkspaceFileEditGuard { Working = true });

        var result = await controller.PutFile(workspaceId, new WorkspaceFileWriteRequest("README.md", "y", "any"));

        Assert.That(((ProblemDetails)((ObjectResult)result).Value!).Title, Is.EqualTo("Workspace agent is working"));
    }

    private static int? StatusOf(IActionResult result) => ((ObjectResult)result).StatusCode;

    private static FakeWorkspaceFileSystemProvider Disk(string path, string text)
    {
        var disk = new FakeWorkspaceFileSystemProvider();
        disk.Files[path] = Encoding.UTF8.GetBytes(text);
        return disk;
    }

    private static async Task<(WorkspaceFilesController Controller, string WorkspaceId)> ComposeAsync(
        FakeWorkspaceFileSystemProvider files,
        FakeWorkspaceFileEditGuard guard)
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var controller = new WorkspaceFilesController(WorkspaceFilesComposition.Service(workspaces, files, new FakeWorkspaceFileChangeProvider(), guard))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return (controller, workspace.Id);
    }
}
