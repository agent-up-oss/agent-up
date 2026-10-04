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
}
