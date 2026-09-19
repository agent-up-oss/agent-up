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

    [Test]
    public async Task DiscardAsync_mapsTheSelectedFiles()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.DiscardAsync("ws-1", ["a.cs"]);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.DiscardedRequest!.Files, Is.EqualTo(new[] { "a.cs" }));
    }

    [Test]
    public async Task SwitchBranchAsync_mapsTheBranchName()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.SwitchBranchAsync("ws-1", "topic", true);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.BranchRequest!.Name, Is.EqualTo("topic"));
        Assert.That(client.BranchRequest.Create, Is.True);
    }

    [Test]
    public async Task CheckoutRemoteAsync_mapsTheBranchName()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.CheckoutRemoteAsync("ws-1", "origin/topic");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.CheckoutRequest!.Name, Is.EqualTo("origin/topic"));
    }

    [Test]
    public async Task FetchAsync_mapsToTheFetchRoute()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.FetchAsync("ws-1");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.FetchRequest, Is.Not.Null);
    }

    [Test]
    public async Task PullAsync_mapsTheRebaseFlag()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.PullAsync("ws-1", rebase: true);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.PullRequest!.Rebase, Is.True);
    }

    [Test]
    public async Task PushAsync_mapsForceWithLease()
    {
        var client = new FakeGitApiProvider();
        var controller = CreateController(client);

        var result = await controller.PushAsync("ws-1", forceWithLease: true);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(client.PushRequest!.ForceWithLease, Is.True);
    }

    private static GitController CreateController(FakeGitApiProvider client)
        => new(new GitChangeListService(client));
}
