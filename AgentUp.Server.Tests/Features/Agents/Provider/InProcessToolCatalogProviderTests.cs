using System.Text.Json;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class InProcessToolCatalogProviderTests
{
    [Test]
    public void Describe_hidesTheWorkspaceTargetFromTheModel()
    {
        var tool = new InProcessToolCatalogProvider().Describe(Json("""
            {"name":"browser_click","description":"Click.","inputSchema":{"type":"object",
             "properties":{"workspaceId":{"type":"string"},"worktreePath":{"type":"string"},"selector":{"type":"string"}},
             "required":["workspaceId","selector"]}}
            """));

        var schema = tool!.Parameters;
        Assert.Multiple(() =>
        {
            Assert.That(tool.Name, Is.EqualTo("browser_click"));
            Assert.That(schema.GetProperty("properties").TryGetProperty("workspaceId", out _), Is.False);
            Assert.That(schema.GetProperty("properties").TryGetProperty("worktreePath", out _), Is.False);
            Assert.That(schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()), Is.EqualTo(new[] { "selector" }));
        });
    }

    [Test]
    public void Describe_skipsToolsThatCannotBePinnedToAWorkspace()
    {
        var tool = new InProcessToolCatalogProvider().Describe(Json("""
            {"name":"list_workspaces","inputSchema":{"type":"object","properties":{}}}
            """));

        Assert.That(tool, Is.Null);
    }

    [Test]
    public void Pin_overwritesAnyWorkspaceTheModelNamed()
    {
        var pinned = new InProcessToolCatalogProvider().Pin(
            Json("""{"workspaceId":"someone-else","worktreePath":"/other","selector":"#save"}"""),
            "pm-1");

        Assert.Multiple(() =>
        {
            Assert.That(pinned["workspaceId"].GetString(), Is.EqualTo("pm-1"));
            Assert.That(pinned.ContainsKey("worktreePath"), Is.False);
            Assert.That(pinned["selector"].GetString(), Is.EqualTo("#save"));
        });
    }

    [Test]
    public void Read_keepsTextAndScreenshotImages()
    {
        var result = new InProcessToolCatalogProvider().Read(Json("""
            {"content":[{"type":"text","text":"captured"},{"type":"image","data":"aGVsbG8=","mimeType":"image/png"}],"isError":false}
            """));

        Assert.Multiple(() =>
        {
            Assert.That(result.Text, Is.EqualTo("captured"));
            Assert.That(result.Images, Is.EqualTo(new[] { "aGVsbG8=" }));
            Assert.That(result.IsError, Is.False);
        });
    }

    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
}
