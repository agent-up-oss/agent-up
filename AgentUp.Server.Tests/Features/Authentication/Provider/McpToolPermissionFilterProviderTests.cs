using AgentUp.Server.Features.Authentication.DTOs;
using AgentUp.Server.Features.Authentication.Providers;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class McpToolPermissionFilterProviderTests
{
    [Test]
    public void Apply_RemovesToolsTheTokenDoesNotGrant()
    {
        var options = OptionsWith("get_commits_status", "enqueue_commit", "start_workspace");

        new McpToolPermissionFilterProvider().Apply(options, Granted(OperationPermissions.GitRead));

        Assert.That(options.ToolCollection!.PrimitiveNames, Is.EquivalentTo(new[] { "get_commits_status" }));
    }

    [Test]
    public void Apply_KeepsEveryToolWhenTheTokenGrantsEveryPermission()
    {
        var options = OptionsWith("get_commits_status", "enqueue_commit", "start_workspace");

        new McpToolPermissionFilterProvider().Apply(options, Granted([.. OperationPermissions.All]));

        Assert.That(
            options.ToolCollection!.PrimitiveNames,
            Is.EquivalentTo(new[] { "get_commits_status", "enqueue_commit", "start_workspace" }));
    }

    /// <summary>
    /// The endpoint allowlist runs first and this filter only removes from what it produced, so a
    /// permission can never put a tool back that the endpoint already dropped.
    /// </summary>
    [Test]
    public void Apply_CannotWidenAnEndpointThatAlreadyDroppedTheTool()
    {
        var options = OptionsWith("get_commits_status");

        new McpToolPermissionFilterProvider().Apply(options, Granted([.. OperationPermissions.All]));

        Assert.That(options.ToolCollection!.PrimitiveNames, Does.Not.Contain("start_workspace"));
    }

    [Test]
    public void Apply_AddsACallFilterSoARememberedToolNameIsStillRefused()
    {
        var options = OptionsWith("get_commits_status");

        new McpToolPermissionFilterProvider().Apply(options, Granted(OperationPermissions.GitRead));

        Assert.That(options.Filters.Request.CallToolFilters, Has.Count.EqualTo(1));
    }

    [Test]
    public void Apply_IgnoresASessionWithNoToolCollection()
    {
        var options = new McpServerOptions { ToolCollection = null };

        Assert.DoesNotThrow(() =>
            new McpToolPermissionFilterProvider().Apply(options, Granted(OperationPermissions.GitRead)));
    }

    [Test]
    public void IsAllowed_RequiresTheMappedPermission()
    {
        var granted = Granted(OperationPermissions.GitRead);

        Assert.Multiple(() =>
        {
            Assert.That(McpToolPermissionFilterProvider.IsAllowed("guard_commits", granted), Is.True);
            Assert.That(McpToolPermissionFilterProvider.IsAllowed("enqueue_commit", granted), Is.False);
        });
    }

    [Test]
    public void IsAllowed_FailsClosedForAnUnmappedOrMissingToolName()
    {
        var granted = Granted([.. OperationPermissions.All]);

        Assert.Multiple(() =>
        {
            Assert.That(McpToolPermissionFilterProvider.IsAllowed("not_a_tool", granted), Is.False);
            Assert.That(McpToolPermissionFilterProvider.IsAllowed(null, granted), Is.False);
        });
    }

    [Test]
    public void Refusal_NamesTheMissingPermissionAndTheTool()
    {
        var refusal = McpToolPermissionFilterProvider.Refusal("enqueue_commit", Granted(OperationPermissions.GitRead));

        Assert.Multiple(() =>
        {
            Assert.That(refusal, Does.Contain("Missing permission 'git.write' for tool 'enqueue_commit'."));
            Assert.That(refusal, Does.Contain("git.read"));
        });
    }

    [Test]
    public void Refusal_ExplainsAnUndeclaredToolAndAnEmptyGrant()
    {
        var undeclared = McpToolPermissionFilterProvider.Refusal("not_a_tool", Granted());
        var unnamed = McpToolPermissionFilterProvider.Refusal(null, Granted());

        Assert.Multiple(() =>
        {
            Assert.That(undeclared, Does.Contain("declares no operation permission"));
            Assert.That(unnamed, Does.Contain("(unnamed)"));
        });
    }

    [Test]
    public void Refusal_ListsAnEmptyGrantInWords()
    {
        var refusal = McpToolPermissionFilterProvider.Refusal("guard_commits", Granted());

        Assert.That(refusal, Does.Contain("no operation permissions"));
    }

    private static IReadOnlySet<string> Granted(params string[] permissions)
        => permissions.ToHashSet(StringComparer.Ordinal);

    private static McpServerOptions OptionsWith(params string[] toolNames)
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var name in toolNames)
            collection.Add(McpServerTool.Create(() => "ok", new McpServerToolCreateOptions { Name = name }));

        return new McpServerOptions { ToolCollection = collection };
    }
}
