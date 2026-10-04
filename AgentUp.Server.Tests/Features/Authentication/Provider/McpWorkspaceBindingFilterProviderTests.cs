using System.Text.Json;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Protocol;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class McpWorkspaceBindingFilterProviderTests
{
    [Test]
    public void HideWorkspaceParameters_ReturnsTheSameResultWhenToolsAreMissing()
    {
        var provider = CreateProvider();
        var result = new ListToolsResult { Tools = null! };

        var hidden = provider.HideWorkspaceParameters(result);

        Assert.That(hidden.Tools, Is.Null);
    }

    [Test]
    public void FilterListedWorkspaces_KeepsOnlyTheBoundWorkspaceInStructuredContent()
    {
        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToElement(new[]
            {
                new { id = "ws-a" },
                new { id = "ws-b" }
            }),
            Content = null!
        };

        McpWorkspaceBindingFilterProvider.FilterListedWorkspaces(result, "ws-a");

        var kept = result.StructuredContent as JsonElement? ?? default;
        Assert.That(
            kept.EnumerateArray().Select(item => item.GetProperty("id").GetString()),
            Is.EqualTo(["ws-a"]));
    }

    [Test]
    public void FilterListedWorkspaces_LeavesNonArrayTextAndEmptyBlocksUnchanged()
    {
        var empty = new TextContentBlock { Text = " " };
        var objectJson = new TextContentBlock { Text = """{"id":"ws-b"}""" };
        var invalid = new TextContentBlock { Text = "not-json" };
        var image = new ImageContentBlock { MimeType = "image/png", Data = new byte[] { 1 } };
        var result = new CallToolResult { Content = [empty, objectJson, invalid, image] };

        McpWorkspaceBindingFilterProvider.FilterListedWorkspaces(result, "ws-a");

        Assert.Multiple(() =>
        {
            Assert.That(((TextContentBlock)result.Content![0]).Text, Is.EqualTo(" "));
            Assert.That(((TextContentBlock)result.Content[1]).Text, Is.EqualTo("""{"id":"ws-b"}"""));
            Assert.That(((TextContentBlock)result.Content[2]).Text, Is.EqualTo("not-json"));
            Assert.That(result.Content[3], Is.SameAs(image));
        });
    }

    private static McpWorkspaceBindingFilterProvider CreateProvider()
        => new(new McpWorkspaceArgumentBinder(new Catalog()), new McpWorkspaceSchemaProvider());

    private sealed class Catalog : IBoundWorkspaceCatalog
    {
        public Task<bool> PathTargetsWorkspaceAsync(
            string boundWorkspace,
            string path,
            CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}
