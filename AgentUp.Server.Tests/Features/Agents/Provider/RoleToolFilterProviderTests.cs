using System.Runtime.CompilerServices;
using AgentUp.Server.Features.Agents.Providers;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class RoleToolFilterProviderTests
{
    [Test]
    public void Restrict_leavesTheToolsetWhenTheAllowlistIsEmpty()
    {
        var options = new McpServerOptions();
        var before = options.Filters.Request.CallToolFilters.Count;

        new RoleToolFilterProvider().Restrict(options, []);

        Assert.That(options.Filters.Request.CallToolFilters.Count, Is.EqualTo(before));
    }

    [Test]
    public void Restrict_installsACallFilterWhenTheAllowlistIsNamed()
    {
        var options = new McpServerOptions();
        var before = options.Filters.Request.CallToolFilters.Count;

        new RoleToolFilterProvider().Restrict(options, ["read_file"]);

        Assert.That(options.Filters.Request.CallToolFilters.Count, Is.EqualTo(before + 1));
    }

    [Test]
    public async Task Restrict_removesForbiddenToolsAndRefusesThemAtCallTime()
    {
        var options = OptionsWith("read_file", "write_file");
        new RoleToolFilterProvider().Restrict(options, ["read_file"]);
        var filter = options.Filters.Request.CallToolFilters[^1];

        var allowed = await InvokeAsync(filter, "read_file");
        var refused = await InvokeAsync(filter, "write_file");
        var unnamed = await InvokeAsync(filter, null);

        Assert.Multiple(() =>
        {
            Assert.That(options.ToolCollection!.PrimitiveNames, Is.EquivalentTo(new[] { "read_file" }));
            Assert.That(allowed.IsError, Is.False);
            Assert.That(refused.IsError, Is.True);
            Assert.That(unnamed.IsError, Is.True);
            Assert.That(
                refused.Content.OfType<TextContentBlock>().Single().Text,
                Does.Contain("write_file").And.Contain("allowlist"));
        });
    }

    private static McpServerOptions OptionsWith(params string[] toolNames)
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var name in toolNames)
            collection.Add(McpServerTool.Create(() => "ok", new McpServerToolCreateOptions { Name = name }));
        return new McpServerOptions { ToolCollection = collection };
    }

    private static async Task<CallToolResult> InvokeAsync(
        McpRequestFilter<CallToolRequestParams, CallToolResult> filter,
        string? name)
    {
        var handler = filter((_, _) => ValueTask.FromResult(new CallToolResult { IsError = false }));
        var context = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(
            typeof(RequestContext<CallToolRequestParams>));
        typeof(RequestContext<CallToolRequestParams>).GetProperty(nameof(RequestContext<CallToolRequestParams>.Params))!
            .SetValue(context, name is null ? null : new CallToolRequestParams { Name = name });
        return await handler(context, CancellationToken.None);
    }
}
