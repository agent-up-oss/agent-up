using System.Text.Json;
using AgentUp.Server.Features.Authentication.Providers;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class McpWorkspaceSchemaProviderTests
{
    [Test]
    public void StripWorkspaceIdProperties_RemovesWorkspaceIdAndKeepsOtherProperties()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                workspaceId = new { type = "string", description = "Registered workspace ID." },
                url = new { type = "string" }
            },
            required = new[] { "workspaceId", "url" }
        });

        var stripped = new McpWorkspaceSchemaProvider().StripWorkspaceIdProperties(schema);

        Assert.Multiple(() =>
        {
            Assert.That(stripped.GetProperty("properties").TryGetProperty("workspaceId", out _), Is.False);
            Assert.That(stripped.GetProperty("properties").TryGetProperty("url", out _), Is.True);
            Assert.That(
                stripped.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
                Is.EqualTo(["url"]));
        });
    }

    [Test]
    public void WorkspaceIdPropertyNames_TreatsDescribedIdAsAWorkspaceParameter()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                id = new { type = "string", description = "Registered workspace id." },
                worktreePath = new { type = "string" }
            }
        });

        var names = new McpWorkspaceSchemaProvider().WorkspaceIdPropertyNames(schema);

        Assert.That(names, Is.EqualTo(["id"]));
    }

    [Test]
    public void WorkspaceIdPropertyNames_IgnoresCapabilityPackageIds()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                id = new { type = "string", description = "Capability package id, such as dotnet or node." }
            }
        });

        var names = new McpWorkspaceSchemaProvider().WorkspaceIdPropertyNames(schema);

        Assert.That(names, Is.Empty);
    }

    [Test]
    public void StripWorkspaceIdProperties_RemovesWorkspaceIdWhenRequiredIsAbsent()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                workspaceId = new { type = "string" },
                url = new { type = "string" }
            }
        });

        var stripped = new McpWorkspaceSchemaProvider().StripWorkspaceIdProperties(schema);

        Assert.Multiple(() =>
        {
            Assert.That(stripped.GetProperty("properties").TryGetProperty("workspaceId", out _), Is.False);
            Assert.That(stripped.GetProperty("properties").TryGetProperty("url", out _), Is.True);
        });
    }

    [Test]
    public void WorkspaceIdPropertyNames_ReturnsNothingForANonObjectSchema()
    {
        var names = new McpWorkspaceSchemaProvider().WorkspaceIdPropertyNames(
            JsonSerializer.SerializeToElement("not-an-object"));

        Assert.That(names, Is.Empty);
    }

    [Test]
    public void StripWorkspaceIdProperties_LeavesSchemasWithoutWorkspaceParametersUnchanged()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { url = new { type = "string" } }
        });

        var stripped = new McpWorkspaceSchemaProvider().StripWorkspaceIdProperties(schema);

        Assert.That(stripped.GetRawText(), Is.EqualTo(schema.GetRawText()));
    }

    [Test]
    public void WorkspaceIdPropertyNames_ReturnsNothingWhenPropertiesAreMissingOrNotAnObject()
    {
        var provider = new McpWorkspaceSchemaProvider();
        var missing = JsonSerializer.SerializeToElement(new { type = "object" });
        var notObject = JsonSerializer.SerializeToElement(new { type = "object", properties = new[] { "workspaceId" } });

        Assert.Multiple(() =>
        {
            Assert.That(provider.WorkspaceIdPropertyNames(missing), Is.Empty);
            Assert.That(provider.WorkspaceIdPropertyNames(notObject), Is.Empty);
        });
    }

    [Test]
    public void ReadDescription_ReturnsNullUnlessThePropertyHasAStringDescription()
    {
        var notObject = JsonSerializer.SerializeToElement("workspace");
        var missing = JsonSerializer.SerializeToElement(new { type = "string" });
        var numeric = JsonSerializer.SerializeToElement(new { type = "string", description = 1 });
        var text = JsonSerializer.SerializeToElement(new { type = "string", description = "Registered workspace ID." });

        Assert.Multiple(() =>
        {
            Assert.That(McpWorkspaceSchemaProvider.ReadDescription(notObject), Is.Null);
            Assert.That(McpWorkspaceSchemaProvider.ReadDescription(missing), Is.Null);
            Assert.That(McpWorkspaceSchemaProvider.ReadDescription(numeric), Is.Null);
            Assert.That(McpWorkspaceSchemaProvider.ReadDescription(text), Is.EqualTo("Registered workspace ID."));
        });
    }

    [Test]
    public void StripWorkspaceIdProperties_IgnoresNullRequiredEntries()
    {
        using var document = JsonDocument.Parse(
            """{"type":"object","properties":{"workspaceId":{"type":"string"},"url":{"type":"string"}},"required":[null,"workspaceId","url"]}""");
        var schema = document.RootElement.Clone();

        var stripped = new McpWorkspaceSchemaProvider().StripWorkspaceIdProperties(schema);

        Assert.That(
            stripped.GetProperty("required").EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null ? null : item.GetString()),
            Is.EqualTo(new string?[] { null, "url" }));
    }
}
