using AgentUp.Desktop.Features.Files.Controllers;
using AgentUp.Desktop.Features.Files.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Files.Controller;

[TestFixture]
public sealed class WorkspaceFilesControllerTests
{
    [Test]
    public async Task GetDirectory_asksForTheNamedFolderOnly()
    {
        var api = new FakeWorkspaceFilesApi().Folder("apps", FakeWorkspaceFilesApi.Leaf("apps/readme.md"));

        var listing = await Controller(api).GetDirectoryAsync(DesktopDomain.WorkspaceId, "apps");

        Assert.That(listing!.Entries.Single().Name, Is.EqualTo("readme.md"));
        Assert.That(api.Requested, Is.EqualTo(new[] { "apps" }));
    }

    [Test]
    public async Task Save_sendsTheVersionTheEditStartedFrom()
    {
        var api = new FakeWorkspaceFilesApi();

        await Controller(api).SaveAsync(DesktopDomain.WorkspaceId, DesktopDomain.FileReadme, "# Edited\n", "v-1");

        Assert.That(api.Saved.Single().Version, Is.EqualTo("v-1"));
    }

    private static WorkspaceFilesController Controller(FakeWorkspaceFilesApi api) => new(new WorkspaceFilesService(api));
}
