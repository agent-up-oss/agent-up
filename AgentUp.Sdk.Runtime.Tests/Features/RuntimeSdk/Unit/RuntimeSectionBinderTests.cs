using System.Text.Json;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Sdk.Runtime.Tests.Features.RuntimeSdk.Unit;

[TestFixture]
public sealed class RuntimeSectionBinderTests
{
    [Test]
    public void Bind_rejects_unknown_extra_keys()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement("api"),
                ["shell"] = JsonSerializer.SerializeToElement("bash")
            }
        };

        var result = RuntimeSectionBinder.Bind(items, [new RuntimeAttributeSpec("project", true, "path")]);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages, Has.Some.Contain("shell"));
    }

    [Test]
    public void Bind_rejects_a_missing_required_extra_attribute()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement("api")
            }
        };

        var result = RuntimeSectionBinder.Bind(items, [new RuntimeAttributeSpec("project", true, "path")]);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("project"));
    }

    [Test]
    public void Bind_accepts_common_and_declared_extra_attributes()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement("api"),
                ["sdk"] = JsonSerializer.SerializeToElement("10.0.x"),
                ["project"] = JsonSerializer.SerializeToElement("Api.csproj")
            }
        };

        var result = RuntimeSectionBinder.Bind(items, [new RuntimeAttributeSpec("project", true, "path")]);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Items.Single()["project"], Is.EqualTo("Api.csproj"));
    }

    [Test]
    public void Bind_promotes_nested_run_project_before_validating()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement("api"),
                ["run"] = JsonSerializer.SerializeToElement(new { project = "Api.csproj" })
            }
        };

        var result = RuntimeSectionBinder.Bind(items, [new RuntimeAttributeSpec("project", true, "path"), new RuntimeAttributeSpec("run", false, "object")]);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Items.Single()["project"], Is.EqualTo("Api.csproj"));
    }
}
