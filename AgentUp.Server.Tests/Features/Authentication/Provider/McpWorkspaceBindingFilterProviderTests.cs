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

    [Test]
    public void FilterListedWorkspaces_LeavesNonArrayStructuredContentUnchanged()
    {
        var structured = JsonSerializer.SerializeToElement(new { id = "ws-b" });
        var result = new CallToolResult { StructuredContent = structured, Content = [] };

        McpWorkspaceBindingFilterProvider.FilterListedWorkspaces(result, "ws-a");

        Assert.That(((JsonElement)result.StructuredContent!).GetRawText(), Is.EqualTo(structured.GetRawText()));
    }

    [Test]
    public void IsBoundWorkspace_RequiresAMatchingObjectId()
    {
        var number = JsonSerializer.SerializeToElement(1);
        var unnamed = JsonSerializer.SerializeToElement(new { name = "ws-a" });
        var other = JsonSerializer.SerializeToElement(new { id = "ws-b" });
        var bound = JsonSerializer.SerializeToElement(new { id = "ws-a" });

        Assert.Multiple(() =>
        {
            Assert.That(McpWorkspaceBindingFilterProvider.IsBoundWorkspace(number, "ws-a"), Is.False);
            Assert.That(McpWorkspaceBindingFilterProvider.IsBoundWorkspace(unnamed, "ws-a"), Is.False);
            Assert.That(McpWorkspaceBindingFilterProvider.IsBoundWorkspace(other, "ws-a"), Is.False);
            Assert.That(McpWorkspaceBindingFilterProvider.IsBoundWorkspace(bound, "ws-a"), Is.True);
        });
    }

    [Test]
    public void ToolInputSchema_ReturnsDefaultWhenTheMatchedPrimitiveIsNotATool()
    {
        Assert.Multiple(() =>
        {
            Assert.That(McpWorkspaceBindingFilterProvider.ToolInputSchema(null).ValueKind, Is.EqualTo(JsonValueKind.Undefined));
            Assert.That(McpWorkspaceBindingFilterProvider.ToolInputSchema("inspect").ValueKind, Is.EqualTo(JsonValueKind.Undefined));
        });
    }

    [Test]
    public void CopyArguments_IgnoresAMissingArgumentMap()
    {
        var destination = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        McpWorkspaceBindingFilterProvider.CopyArguments(null, destination);

        Assert.That(destination, Is.Empty);
    }

    [Test]
    public void CopyArguments_CopiesSuppliedValues()
    {
        var destination = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var supplied = new Dictionary<string, JsonElement>
        {
            ["url"] = JsonSerializer.SerializeToElement("https://example.test")
        };

        McpWorkspaceBindingFilterProvider.CopyArguments(supplied, destination);

        Assert.That(destination["url"].GetString(), Is.EqualTo("https://example.test"));
    }

    [Test]
    public void WriteBoundArguments_IgnoresAMissingRequest()
    {
        Assert.DoesNotThrow(() => McpWorkspaceBindingFilterProvider.WriteBoundArguments(
            null,
            new Dictionary<string, JsonElement>()));
    }

    [Test]
    public void WriteBoundArguments_ReplacesTheRequestArguments()
    {
        var parameters = new CallToolRequestParams { Name = "browser_inspect" };
        var arguments = new Dictionary<string, JsonElement>
        {
            ["workspaceId"] = JsonSerializer.SerializeToElement("ws-a")
        };

        McpWorkspaceBindingFilterProvider.WriteBoundArguments(parameters, arguments);

        Assert.That(parameters.Arguments!["workspaceId"].GetString(), Is.EqualTo("ws-a"));
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
