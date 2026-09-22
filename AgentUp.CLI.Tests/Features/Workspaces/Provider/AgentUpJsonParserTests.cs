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

    // `name` has to be a JSON string. A number there is a malformed file, not a workspace named
    // "5", and the CLI must say so rather than forward it.
    [Test]
    public void Parse_rejects_a_name_that_is_not_a_string()
    {
        Assert.That(
            () => AgentUpJsonParser.Parse("""{"name":5}"""),
            Throws.InvalidOperationException.With.Message.Contains("name"));
    }

    [Test]
    public void Parse_names_an_item_that_does_not_name_itself()
    {
        var parsed = AgentUpJsonParser.Parse("""{"name":"App","python":[{"script":"main.py"}]}""");

        Assert.That(parsed.RuntimeSections!.Single().Items.Single().Name, Is.EqualTo("application"));
    }

    // A module may spell its launch as `command` instead of `arguments`; both reach the Server as
    // the item's extra arguments, and a single string is one argument rather than a character
    // sequence.
    [TestCase("""{"name":"App","python":[{"name":"w","command":["python","main.py"]}]}""", new[] { "python", "main.py" })]
    [TestCase("""{"name":"App","python":[{"name":"w","command":"python main.py"}]}""", new[] { "python main.py" })]
    [TestCase("""{"name":"App","python":[{"name":"w","command":"   "}]}""", new string[0])]
    public void Parse_reads_a_command_in_either_shape(string json, string[] expected)
    {
        var parsed = AgentUpJsonParser.Parse(json);

        Assert.That(parsed.RuntimeSections!.Single().Items.Single().ExtraArguments, Is.EqualTo(expected));
    }

    // `arguments` is what the item declares first, so a `command` beside it is a parameter and not
    // a second source of launch arguments.
    [Test]
    public void Parse_prefers_arguments_over_command()
    {
        var parsed = AgentUpJsonParser.Parse(
            """{"name":"App","python":[{"name":"w","arguments":["--once"],"command":"ignored"}]}""");

        Assert.That(parsed.RuntimeSections!.Single().Items.Single().ExtraArguments, Is.EqualTo(new[] { "--once" }));
    }

    [Test]
    public void Parse_reads_a_non_array_non_string_list_as_its_raw_text()
    {
        var parsed = AgentUpJsonParser.Parse("""{"name":"App","python":[{"name":"w","command":{"run":"x"}}]}""");

        Assert.That(
            parsed.RuntimeSections!.Single().Items.Single().ExtraArguments,
            Is.EqualTo(new[] { """{"run":"x"}""" }));
    }

    [Test]
    public void Parse_reads_non_string_list_entries_as_their_raw_text()
    {
        var parsed = AgentUpJsonParser.Parse("""{"name":"App","python":[{"name":"w","arguments":[1,"--once"]}]}""");

        Assert.That(
            parsed.RuntimeSections!.Single().Items.Single().ExtraArguments,
            Is.EqualTo(new[] { "1", "--once" }));
    }

    // Every attribute reaches the module as a string parameter, so each JSON kind has to have one
    // spelling rather than whatever `ToString` happens to produce.
    [Test]
    public void Parse_flattens_every_attribute_kind_to_a_parameter_string()
    {
        var parsed = AgentUpJsonParser.Parse(
            """
            {"name":"App","python":[{"name":"w","text":"x","yes":true,"no":false,"count":3,
             "nothing":null,"nested":{"a":1}}]}
            """);

        var parameters = parsed.RuntimeSections!.Single().Items.Single().Parameters!;
        Assert.Multiple(() =>
        {
            Assert.That(parameters["text"], Is.EqualTo("x"));
            Assert.That(parameters["yes"], Is.EqualTo("true"));
            Assert.That(parameters["no"], Is.EqualTo("false"));
            Assert.That(parameters["count"], Is.EqualTo("3"));
            Assert.That(parameters["nothing"], Is.Empty);
            Assert.That(parameters["nested"], Is.EqualTo("""{"a":1}"""));
        });
    }

    [Test]
    public void Parse_reads_database_only_from_a_literal_true()
    {
        var parsed = AgentUpJsonParser.Parse(
            """{"name":"App","docker":[{"name":"a","image":"x","database":false},{"name":"b","image":"y"}]}""");

        Assert.That(parsed.Docker!.Select(item => item.Database), Is.EqualTo(new[] { false, false }));
    }

    // A blank explicit project is not a value, so the nested one still wins; a `run` that is not
    // an object contributes nothing at all.
    [TestCase("""{"name":"App","dotnet":[{"name":"a","project":"   ","run":{"project":"Nested.csproj"}}]}""", "Nested.csproj")]
    [TestCase("""{"name":"App","dotnet":[{"name":"a","project":null,"run":{"project":"Nested.csproj"}}]}""", "Nested.csproj")]
    [TestCase("""{"name":"App","dotnet":[{"name":"a","run":"Nested.csproj"}]}""", "")]
    public void Parse_promotes_the_nested_run_only_where_nothing_was_declared(string json, string expected)
    {
        var parsed = AgentUpJsonParser.Parse(json);

        Assert.That(parsed.Dotnet!.Single().Run!.Project, Is.EqualTo(expected));
    }

    [Test]
    public void Parse_reads_an_explicit_null_display_as_no_display()
    {
        var parsed = AgentUpJsonParser.Parse("""{"name":"App","display":null}""");

        Assert.That(parsed.Display, Is.Null);
    }

    [Test]
    public void Parse_reads_the_display_overrides()
    {
        var parsed = AgentUpJsonParser.Parse("""{"name":"App","display":{"name":"Shown","branch":"main"}}""");

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Display!.Name, Is.EqualTo("Shown"));
            Assert.That(parsed.Display.Branch, Is.EqualTo("main"));
        });
    }
}
