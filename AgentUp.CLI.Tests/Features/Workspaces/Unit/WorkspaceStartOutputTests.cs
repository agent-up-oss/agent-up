using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Services;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Workspaces.Unit;

/// <summary>
/// What `agent-up start` prints back is the only place a developer sees which capability
/// modules the Server matched their `agent-up.json` sections to.
/// </summary>
[TestFixture]
public sealed class WorkspaceStartOutputTests
{
    [Test]
    public void Start_reports_the_error_rather_than_a_workspace()
    {
        using var output = new StringWriter();
        var service = new WorkspaceCommandOutputService(output);

        var exitCode = service.WriteStartResult(WorkspaceCommandResult<StartedWorkspace>.Failed("Error: no Server."));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Error: no Server."));
        });
    }

    // dotnet and docker are already printed under their product headings, so reprinting them as
    // runtime sections would list every one of them twice.
    [Test]
    public void Start_does_not_repeat_the_dotnet_and_docker_sections_as_runtime_sections()
    {
        using var output = new StringWriter();

        WriteStart(
            output,
            [
                Section("dotnet", Item("Example API", ("project", "ExampleApi.csproj"))),
                Section("docker", Item("Database", ("image", "postgres:16")))
            ],
            dotnet: [new DotnetApplicationDefinition("Example API", null, new DotnetRunDefinition("ExampleApi.csproj", null))],
            docker: [new DockerCapabilityDefinition("Database", "postgres:16")]);

        var printed = output.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(Occurrences(printed, "Example API"), Is.EqualTo(1));
            Assert.That(Occurrences(printed, "Database"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Start_lists_an_unknown_module_section_under_its_module_id()
    {
        using var output = new StringWriter();

        WriteStart(output, [Section("python", Item("worker", ("script", "main.py")))]);

        Assert.That(output.ToString(), Does.Contain("python (1):").And.Contain("- worker: main.py"));
    }

    // One line per item, and the most specific thing the item declared is what identifies it.
    [TestCase("image", "postgres:16", "db: postgres:16")]
    [TestCase("project", "Api.csproj", "db: Api.csproj")]
    [TestCase("script", "main.py", "db: main.py")]
    [TestCase("nothing", "at all", "db")]
    [TestCase("image", "   ", "db")]
    public void Start_identifies_a_runtime_item_by_what_it_declared(string key, string value, string expected)
    {
        using var output = new StringWriter();

        WriteStart(output, [Section("python", Item("db", (key, value)))]);

        Assert.That(output.ToString(), Does.Contain("    - " + expected));
    }

    [Test]
    public void Start_identifies_an_item_with_no_parameters_by_its_name_alone()
    {
        using var output = new StringWriter();

        WriteStart(output, [Section("python", new RuntimeSectionItem("bare"))]);

        Assert.That(output.ToString(), Does.Contain("    - bare"));
    }

    [Test]
    public void Start_lists_the_desktop_applications_it_started()
    {
        using var output = new StringWriter();

        WriteStart(
            output,
            [],
            desktopApplications: [new DesktopApplicationDefinition("Sample Desktop", "dotnet run", ".")]);

        Assert.That(output.ToString(), Does.Contain("Desktop applications (1):").And.Contain("- Sample Desktop: dotnet run"));
    }

    private static void WriteStart(
        TextWriter output,
        IReadOnlyList<RuntimeSectionDefinition> runtimeSections,
        IReadOnlyList<DesktopApplicationDefinition>? desktopApplications = null,
        IReadOnlyList<DotnetApplicationDefinition>? dotnet = null,
        IReadOnlyList<DockerCapabilityDefinition>? docker = null)
    {
        var service = new WorkspaceCommandOutputService(output);
        var started = new StartedWorkspace(
            CliDomain.Workspace().Build(),
            [],
            desktopApplications ?? [],
            [],
            runtimeSections,
            dotnet ?? [],
            docker ?? []);

        service.WriteStartResult(WorkspaceCommandResult<StartedWorkspace>.Success(started));
    }

    private static RuntimeSectionDefinition Section(string moduleId, params RuntimeSectionItem[] items)
        => new(moduleId, items);

    private static RuntimeSectionItem Item(string name, params (string Key, string Value)[] parameters)
        => new(
            name,
            Parameters: parameters.ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.Value,
                StringComparer.OrdinalIgnoreCase));

    private static int Occurrences(string text, string value)
        => text.Split(value).Length - 1;
}
