using AgentUp.CLI.Features.Workspaces.Providers;

namespace AgentUp.CLI.Tests.Features.Workspaces.Provider;

/// <summary>
/// The CLI forwards named runtime sections to the Server without binding them, so what it reads
/// out of `agent-up.json` is the whole of its contribution to a capability-hosted workspace.
/// </summary>
[TestFixture]
public sealed class AgentUpJsonParserTests
{
    [Test]
    public void Parse_rejects_a_document_that_is_not_an_object()
    {
        Assert.That(
            () => AgentUpJsonParser.Parse("[]"),
            Throws.InvalidOperationException.With.Message.Contains("JSON object"));
    }

    [Test]
    public void Parse_rejects_a_document_with_no_name()
    {
        Assert.That(
            () => AgentUpJsonParser.Parse("""{"applications":[]}"""),
            Throws.InvalidOperationException.With.Message.Contains("name"));
    }

    [Test]
    public void Parse_rejects_a_runtime_section_item_that_is_not_an_object()
    {
        Assert.That(
            () => AgentUpJsonParser.Parse("""{"name":"App","dotnet":["Api.csproj"]}"""),
            Throws.InvalidOperationException.With.Message.Contains("JSON objects"));
    }

    // Reserved keys are the legacy shapes, not module ids, so they must not be forwarded as
    // runtime sections however much they look like one.
    [Test]
    public void Parse_does_not_treat_reserved_arrays_as_runtime_sections()
    {
        var parsed = AgentUpJsonParser.Parse(
            """{"name":"App","applications":[{"name":"Docs","command":"npm run start"}],"services":[]}""");

        Assert.That(parsed.RuntimeSections, Is.Empty);
        Assert.That(parsed.Applications!.Single().Name, Is.EqualTo("Docs"));
    }

    [Test]
    public void Parse_maps_the_dotnet_section_onto_the_legacy_dotnet_applications()
    {
        var parsed = AgentUpJsonParser.Parse(
            """
            {"name":"App","dotnet":[{"name":"Example API","sdk":"10.0.x","path":"api",
             "run":{"project":"ExampleApi.csproj","arguments":["--no-launch-profile"]},
             "environmentFiles":["database.env"],"database":true}]}
            """);

        var api = parsed.Dotnet!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(api.Name, Is.EqualTo("Example API"));
            Assert.That(api.Sdk, Is.EqualTo("10.0.x"));
            Assert.That(api.Run!.Project, Is.EqualTo("ExampleApi.csproj"));
            Assert.That(api.Run.Arguments, Is.EqualTo(new[] { "--no-launch-profile" }));
            Assert.That(api.EnvironmentFiles, Is.EqualTo(new[] { "database.env" }));
            Assert.That(api.Database, Is.True);
        });
    }

    [Test]
    public void Parse_maps_the_docker_section_onto_the_legacy_docker_capabilities()
    {
        var parsed = AgentUpJsonParser.Parse(
            """
            {"name":"App","docker":[{"name":"Database","image":"postgres:16","database":true,
             "volumes":["pgdata:/var/lib/postgresql/data"],"environment":{"POSTGRES_DB":"app"}}]}
            """);

        var database = parsed.Docker!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(database.Name, Is.EqualTo("Database"));
            Assert.That(database.Image, Is.EqualTo("postgres:16"));
            Assert.That(database.Volumes, Is.EqualTo(new[] { "pgdata:/var/lib/postgresql/data" }));
            Assert.That(database.Environment!["POSTGRES_DB"], Is.EqualTo("app"));
            Assert.That(database.Database, Is.True);
        });
    }

    // An explicit project wins over the one nested under `run`, so promoting the nested shape
    // cannot quietly rewrite what the file already said.
    [Test]
    public void Parse_keeps_an_explicit_project_over_the_one_nested_under_run()
    {
        var parsed = AgentUpJsonParser.Parse(
            """{"name":"App","dotnet":[{"name":"Api","project":"Explicit.csproj","run":{"project":"Nested.csproj"}}]}""");

        Assert.That(parsed.Dotnet!.Single().Run!.Project, Is.EqualTo("Explicit.csproj"));
    }

    [Test]
    public void Parse_forwards_an_unknown_module_section_untouched()
    {
        var parsed = AgentUpJsonParser.Parse(
            """{"name":"App","python":[{"name":"worker","script":"main.py","arguments":["--once"]}]}""");

        var section = parsed.RuntimeSections!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(section.ModuleId, Is.EqualTo("python"));
            Assert.That(section.Items.Single().Parameters!["script"], Is.EqualTo("main.py"));
            Assert.That(section.Items.Single().ExtraArguments, Is.EqualTo(new[] { "--once" }));
        });
    }
}
