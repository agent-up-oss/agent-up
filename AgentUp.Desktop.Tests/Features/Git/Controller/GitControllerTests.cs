using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Services;
using AgentUp.Desktop.Tests.Features.Git.Unit;

namespace AgentUp.Desktop.Tests.Features.Git.Controller;

[TestFixture]
public sealed class GitControllerTests
{
    [Test]
    public async Task GetChangesAsync_delegatesToTheService()
    {
        var client = new FakeGitApiProvider
        {
            Tree = new GitChangeTreeDto("ws-1", "main", 0,
                new GitChangeDirectoryDto(string.Empty, string.Empty, [], []))
        };
        var controller = CreateController(client);

        var tree = await controller.GetChangesAsync("ws-1");

        Assert.That(tree!.Branch, Is.EqualTo("main"));
        Assert.That(client.ChangeRequests, Is.EqualTo(1));
    }

    [Test]
    public async Task GetFileDiffAsync_delegatesToTheService()
    {
        var client = new FakeGitApiProvider
        {
            FileDiff = new GitFileDiffDto("a.cs", "Modified", false, "@@")
        };
        var controller = CreateController(client);

        var diff = await controller.GetFileDiffAsync("ws-1", "a.cs");

        Assert.That(diff!.Path, Is.EqualTo("a.cs"));
    }

    [Test]
    public async Task CommitAsync_mapsFilesAndMessageOntoTheRequest()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.CommitAsync("ws-1", ["a.cs", "b.cs"], "chore: touch");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.CommittedRequest!.Files, Is.EqualTo(new[] { "a.cs", "b.cs" }));
        Assert.That(client.CommittedRequest.Message, Is.EqualTo("chore: touch"));
    }

    private static GitController CreateController(FakeGitApiProvider client)
        => new(new GitChangeListService(client));
}
