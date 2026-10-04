using System.Text.Json;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class McpWorkspaceArgumentBinderTests
{
    [Test]
    public async Task BindAsync_PinsMissingWorkspaceIdAndAllowsTheBoundWorkspace()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>(),
            ["workspaceId"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Arguments["workspaceId"].GetString(), Is.EqualTo("ws-a"));
        });
    }

    [Test]
    public async Task BindAsync_RefusesADifferentWorkspaceId()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["workspaceId"] = JsonSerializer.SerializeToElement("ws-b")
            },
            ["workspaceId"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.Error, Does.Contain("ws-b"));
        });
    }

    [Test]
    public async Task BindAsync_RefusesAWorktreePathThatDoesNotBelongToTheBoundWorkspace()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(false));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["worktreePath"] = JsonSerializer.SerializeToElement(ServerDomain.SecondWorktreePath)
            },
            [],
            CancellationToken.None);

        Assert.That(decision.Allowed, Is.False);
    }

    [Test]
    public async Task BindAsync_RefusesADifferentWorkspaceTypedId()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["id"] = JsonSerializer.SerializeToElement("ws-b")
            },
            ["id"],
            CancellationToken.None);

        Assert.That(decision.Allowed, Is.False);
    }

    [Test]
    public async Task BindAsync_AllowsAWorktreePathOwnedByTheBoundWorkspace()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["worktreePath"] = JsonSerializer.SerializeToElement(ServerDomain.WorktreePath)
            },
            [],
            CancellationToken.None);

        Assert.That(decision.Allowed, Is.True);
    }

    [Test]
    public async Task BindAsync_IgnoresBlankAndNonWorkspaceArguments()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["workspaceId"] = JsonSerializer.SerializeToElement(" "),
                ["url"] = JsonSerializer.SerializeToElement("https://example.test"),
                ["count"] = JsonSerializer.SerializeToElement(2)
            },
            ["workspaceId"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Arguments["workspaceId"].GetString(), Is.EqualTo("ws-a"));
            Assert.That(decision.Arguments["url"].GetString(), Is.EqualTo("https://example.test"));
        });
    }

    [Test]
    public async Task BindAsync_RefusesANonStringWorkspaceIdEvenWhenTheSchemaOmitsIt()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["workspaceId"] = JsonSerializer.SerializeToElement(1)
            },
            [],
            CancellationToken.None);

        Assert.That(decision.Allowed, Is.False);
    }

    [Test]
    public async Task BindAsync_RefusesARepositoryPathThatDoesNotBelongToTheBoundWorkspace()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(false));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["repositoryPath"] = JsonSerializer.SerializeToElement(ServerDomain.SecondWorktreePath)
            },
            [],
            CancellationToken.None);

        Assert.That(decision.Allowed, Is.False);
    }

    private sealed class Catalog(bool pathBelongs) : IBoundWorkspaceCatalog
    {
        public Task<bool> PathTargetsWorkspaceAsync(
            string boundWorkspace,
            string path,
            CancellationToken cancellationToken)
            => Task.FromResult(pathBelongs);
    }
}
