using System.Text.Json;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Orchestration.Unit;

[TestFixture]
public sealed class AgentUpConfigurationParserTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public void Parse_binds_root_keys_that_match_enabled_runtime_ids()
    {
        var runtime = PythonRuntime();
        using var document = JsonDocument.Parse(
            """{"name":"App","python":[{"name":"api","script":"main.py"}],"verification":{"always":[]},"applications":[]}""");

        var config = AgentUpConfigurationParser.Parse(document.RootElement, [runtime], Json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Name, Is.EqualTo("App"));
            Assert.That(config.RuntimeSections ?? [], Has.Count.EqualTo(1));
            Assert.That(config.RuntimeSections![0].ModuleId, Is.EqualTo("python"));
            Assert.That(config.RuntimeSections[0].Items.Single().Name, Is.EqualTo("api"));
            Assert.That(config.RuntimeSections[0].Items.Single().Parameters!["script"], Is.EqualTo("main.py"));
        });
    }

    [Test]
    public void Parse_binds_first_party_dotnet_through_the_same_runtime_path()
    {
        var runtime = new StubRuntimeCapability
        {
            ExtraAttributes = [new RuntimeAttributeSpec("project", true, "path"), new RuntimeAttributeSpec("run", false, "object")]
        };
        using var document = JsonDocument.Parse(
            """{"name":"App","dotnet":[{"name":"API","sdk":"10.0.x","run":{"project":"src/Api/Api.csproj"}}]}""");

        var config = AgentUpConfigurationParser.Parse(document.RootElement, [runtime], Json);

        Assert.Multiple(() =>
        {
            Assert.That(config.RuntimeSections ?? [], Has.Count.EqualTo(1));
            Assert.That(config.RuntimeSections![0].ModuleId, Is.EqualTo("dotnet"));
            Assert.That(config.Dotnet ?? [], Has.Count.EqualTo(1));
            Assert.That(config.Dotnet![0].Run.Project, Is.EqualTo("src/Api/Api.csproj"));
            Assert.That(config.Dotnet[0].Sdk, Is.EqualTo("10.0.x"));
        });
    }

    [Test]
    public void Parse_keeps_runtime_sections_whose_module_is_not_enabled()
    {
        using var document = JsonDocument.Parse("""{"name":"App","python":[{"name":"api","script":"main.py"}]}""");

        var config = AgentUpConfigurationParser.Parse(document.RootElement, [], Json);

        Assert.Multiple(() =>
        {
            Assert.That(config.RuntimeSections ?? [], Has.Count.EqualTo(1));
            Assert.That(config.RuntimeSections![0].ModuleId, Is.EqualTo("python"));
            Assert.That(config.RuntimeSections[0].Items.Single().Name, Is.EqualTo("api"));
            Assert.That(config.RuntimeSections[0].Items.Single().Parameters!["script"], Is.EqualTo("main.py"));
        });
    }

    [Test]
    public void Parse_keeps_dotnet_and_docker_when_no_runtime_module_is_enabled()
    {
        using var document = JsonDocument.Parse(
            """{"name":"App","dotnet":[{"name":"SmokeDotnet","sdk":"10.0.x","run":{"project":"SmokeDotnet/SmokeDotnet.csproj"}}],"docker":[{"name":"SmokeDocker","image":"nginx:alpine"}]}""");

        var config = AgentUpConfigurationParser.Parse(document.RootElement, [], Json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Dotnet ?? [], Has.Count.EqualTo(1));
            Assert.That(config.Dotnet![0].Run.Project, Is.EqualTo("SmokeDotnet/SmokeDotnet.csproj"));
            Assert.That(config.Docker ?? [], Has.Count.EqualTo(1));
            Assert.That(config.Docker![0].Image, Is.EqualTo("nginx:alpine"));
        });
    }

    [Test]
    public void Parse_ignores_non_array_root_values_that_no_module_claims()
    {
        using var document = JsonDocument.Parse("""{"name":"App","somethingElse":{"note":"not a runtime section"}}""");

        var config = AgentUpConfigurationParser.Parse(document.RootElement, [], Json);

        Assert.That(config.RuntimeSections ?? [], Is.Empty);
    }

    [Test]
    public void Parse_fails_unknown_extra_keys_and_missing_required_attributes()
    {
        var runtime = PythonRuntime();
        using var unknown = JsonDocument.Parse("""{"name":"App","python":[{"name":"api","script":"main.py","shell":"bash"}]}""");
        using var missing = JsonDocument.Parse("""{"name":"App","python":[{"name":"api"}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(
                () => AgentUpConfigurationParser.Parse(unknown.RootElement, [runtime], Json),
                Throws.InvalidOperationException.With.Message.Contains("shell"));
            Assert.That(
                () => AgentUpConfigurationParser.Parse(missing.RootElement, [runtime], Json),
                Throws.InvalidOperationException.With.Message.Contains("script"));
        });
    }

    private static StubRuntimeCapability PythonRuntime() => new()
    {
        Identity = new CapabilityIdentity("python", "1.0.0", "Python", "agent-up"),
        ExtraAttributes = [new RuntimeAttributeSpec("script", true, "path")]
    };
}
