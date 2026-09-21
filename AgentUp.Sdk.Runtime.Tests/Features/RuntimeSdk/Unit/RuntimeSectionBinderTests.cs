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

    // The bound values are what a module's Host reads, so every JSON shape a section can hold
    // has to arrive as a string rather than as raw JSON the module would have to parse again.
    [Test]
    public void Bind_flattens_every_json_value_kind_a_section_can_carry()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement("api"),
                ["database"] = JsonSerializer.SerializeToElement(true),
                ["sdk"] = JsonSerializer.SerializeToElement(10),
                ["path"] = JsonSerializer.SerializeToElement((string?)null),
                ["ports"] = JsonSerializer.SerializeToElement(new[] { 5601 })
            }
        };

        var bound = RuntimeSectionBinder.Bind(items, []).Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(bound["name"], Is.EqualTo("api"));
            Assert.That(bound["database"], Is.EqualTo("true"));
            Assert.That(bound["sdk"], Is.EqualTo("10"));
            Assert.That(bound["path"], Is.Empty);
            Assert.That(bound["ports"], Is.EqualTo("[5601]"));
        });
    }

    [Test]
    public void Bind_flattens_a_false_value()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["database"] = JsonSerializer.SerializeToElement(false)
            }
        };

        Assert.That(RuntimeSectionBinder.Bind(items, []).Items.Single()["database"], Is.EqualTo("false"));
    }

    [Test]
    public void Bind_reports_every_item_that_is_wrong_rather_than_the_first()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["unexpected"] = JsonSerializer.SerializeToElement("one")
            },
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["surprising"] = JsonSerializer.SerializeToElement("two")
            }
        };

        var result = RuntimeSectionBinder.Bind(items, []);

        Assert.That(result.IsValid, Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(result.Messages, Has.Some.Contain("unexpected"));
            Assert.That(result.Messages, Has.Some.Contain("surprising"));
            Assert.That(result.Items, Has.Count.EqualTo(2), "every item is still bound for reporting");
        });
    }

    // An explicit value wins over the nested one, so promoting `run` cannot rewrite what the
    // file already said.
    [Test]
    public void PromoteNestedRun_keeps_an_explicit_value_over_the_nested_one()
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = JsonSerializer.SerializeToElement("Explicit.csproj"),
            ["run"] = JsonSerializer.SerializeToElement(new { project = "Nested.csproj" })
        };

        var promoted = RuntimeSectionBinder.PromoteNestedRun(item);

        Assert.That(promoted["project"].GetString(), Is.EqualTo("Explicit.csproj"));
    }

    [Test]
    public void PromoteNestedRun_promotes_arguments_as_well_as_the_project()
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["run"] = JsonSerializer.SerializeToElement(new
            {
                project = "Api.csproj",
                arguments = new[] { "--no-launch-profile" }
            })
        };

        var promoted = RuntimeSectionBinder.PromoteNestedRun(item);

        Assert.Multiple(() =>
        {
            Assert.That(promoted["project"].GetString(), Is.EqualTo("Api.csproj"));
            Assert.That(promoted["arguments"].GetRawText(), Does.Contain("--no-launch-profile"));
        });
    }

    [Test]
    public void PromoteNestedRun_leaves_an_item_with_no_run_object_alone()
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["run"] = JsonSerializer.SerializeToElement("not-an-object")
        };

        Assert.That(RuntimeSectionBinder.PromoteNestedRun(item), Is.SameAs(item));
    }

    // A blank string is a missing value, not a supplied one.
    [Test]
    public void Bind_treats_a_blank_required_attribute_as_missing()
    {
        var items = new[]
        {
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["project"] = JsonSerializer.SerializeToElement("   ")
            }
        };

        var result = RuntimeSectionBinder.Bind(items, [new RuntimeAttributeSpec("project", true, "path")]);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Messages, Has.Some.Contain("project"));
    }

    [Test]
    public void PromoteNestedRun_leaves_an_item_with_no_run_at_all_alone()
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = JsonSerializer.SerializeToElement("Api.csproj")
        };

        Assert.That(RuntimeSectionBinder.PromoteNestedRun(item), Is.SameAs(item));
    }

    // A blank explicit value is not a value, so the nested one still reaches the module.
    [Test]
    public void PromoteNestedRun_replaces_a_blank_explicit_value()
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = JsonSerializer.SerializeToElement("   "),
            ["run"] = JsonSerializer.SerializeToElement(new { project = "Nested.csproj" })
        };

        Assert.That(RuntimeSectionBinder.PromoteNestedRun(item)["project"].GetString(), Is.EqualTo("Nested.csproj"));
    }

    // `run` may hold only one of the two, and promoting it must not invent the other.
    [Test]
    public void PromoteNestedRun_promotes_only_what_run_actually_holds()
    {
        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["run"] = JsonSerializer.SerializeToElement(new { project = "Api.csproj" })
        };

        Assert.That(RuntimeSectionBinder.PromoteNestedRun(item).ContainsKey("arguments"), Is.False);
    }
}
