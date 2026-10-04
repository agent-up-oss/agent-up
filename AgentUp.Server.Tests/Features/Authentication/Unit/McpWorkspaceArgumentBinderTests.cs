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

    [Test]
    public async Task BindAsync_FillsAnOmittedWorktreePathFromTheBoundWorkspace()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true, ServerDomain.WorktreePath));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>(),
            ["worktreePath"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Arguments["worktreePath"].GetString(), Is.EqualTo(ServerDomain.WorktreePath));
        });
    }

    [Test]
    public async Task BindAsync_PinsTheWorkspaceIdInsteadOfThePathWhenTheToolTakesBoth()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true, ServerDomain.WorktreePath));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>(),
            ["workspaceId", "worktreePath"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Arguments["workspaceId"].GetString(), Is.EqualTo("ws-a"));
            Assert.That(decision.Arguments.ContainsKey("worktreePath"), Is.False);
        });
    }

    [Test]
    public async Task BindAsync_RefusesAPathOnlyToolWhenTheBoundWorkspaceIsNotRegistered()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>(),
            ["worktreePath"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.False);
            Assert.That(decision.Error, Does.Contain("ws-a"));
            Assert.That(decision.Error, Does.Contain("not registered"));
        });
    }

    [Test]
    public async Task BindAsync_KeepsAWorktreePathTheCallerSuppliedToAPathOnlyTool()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true, ServerDomain.SecondWorktreePath));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["worktreePath"] = JsonSerializer.SerializeToElement(ServerDomain.WorktreePath)
            },
            ["worktreePath"],
            CancellationToken.None);

        Assert.That(decision.Arguments["worktreePath"].GetString(), Is.EqualTo(ServerDomain.WorktreePath));
    }

    [Test]
    public async Task BindAsync_ReplacesTheOwnPathACallerSentWithTheBoundId()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true, ServerDomain.WorktreePath));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["worktreePath"] = JsonSerializer.SerializeToElement(ServerDomain.WorktreePath)
            },
            ["workspaceId", "worktreePath"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Arguments["workspaceId"].GetString(), Is.EqualTo("ws-a"));
            Assert.That(decision.Arguments.ContainsKey("worktreePath"), Is.False);
        });
    }

    [Test]
    public async Task BindAsync_DropsARepositoryPathFilterThatCouldStillReadAcrossWorkspaces()
    {
        var binder = new McpWorkspaceArgumentBinder(new Catalog(true, ServerDomain.WorktreePath));
        var decision = await binder.BindAsync(
            "ws-a",
            new Dictionary<string, JsonElement>
            {
                ["repositoryPath"] = JsonSerializer.SerializeToElement(ServerDomain.RepositoryPath)
            },
            ["workspaceId", "repositoryPath"],
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Arguments["workspaceId"].GetString(), Is.EqualTo("ws-a"));
            Assert.That(decision.Arguments.ContainsKey("repositoryPath"), Is.False);
        });
    }

    private sealed class Catalog(bool pathBelongs, string? worktreePath = null) : IBoundWorkspaceCatalog
    {
        public Task<bool> PathTargetsWorkspaceAsync(
            string boundWorkspace,
            string path,
            CancellationToken cancellationToken)
            => Task.FromResult(pathBelongs);

        public string? WorktreePathFor(string boundWorkspace) => worktreePath;
    }
}
