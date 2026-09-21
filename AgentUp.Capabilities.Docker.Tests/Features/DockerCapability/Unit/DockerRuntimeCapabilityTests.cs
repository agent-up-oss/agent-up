using System.Text.Json;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;
using AgentUp.Sdk.Runtime;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Unit;

[TestFixture]
public sealed class DockerRuntimeCapabilityTests
{
    [Test]
    public void Host_requires_an_image()
    {
        var result = new DockerRuntimeCapability().Host(Request());

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("image"));
    }

    [Test]
    public void Host_builds_docker_run_arguments()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            [new RuntimePortMapping("DB_PORT", 5432, 15432)],
            ["data:/var/lib/postgresql/data"],
            ["postgres"]));

        Assert.That(result.CanRun, Is.True);
        Assert.That(result.FileName, Is.EqualTo("docker"));
        Assert.That(result.Arguments, Does.Contain("postgres:17"));
        Assert.That(result.Arguments, Does.Contain("15432:5432"));
    }

    /// <summary>
    /// A container cannot reach the Server on the host's own loopback address, so the audit
    /// endpoint it is handed has to name the host instead.
    /// </summary>
    [Test]
    public void Host_rewrites_a_loopback_audit_endpoint_so_the_container_can_reach_it()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            auditEndpoint: "http://127.0.0.1:5000/api/audit"));

        Assert.That(result.CanRun, Is.True);
        Assert.That(
            result.Arguments,
            Does.Contain("AGENT_UP_AUDIT_ENDPOINT=http://host.agent-up:5000/api/audit"));
    }

    [Test]
    public void Host_keeps_an_audit_endpoint_that_is_already_reachable()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            auditEndpoint: "https://audit.example.com/api/audit"));

        Assert.That(result.Arguments, Does.Contain("AGENT_UP_AUDIT_ENDPOINT=https://audit.example.com/api/audit"));
    }

    [Test]
    public void Host_passes_environment_files_environment_and_workspace_identity_to_the_container()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            environment: new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = "secret" },
            environmentFilePaths: ["/repo/database.env"]));

        Assert.Multiple(() =>
        {
            Assert.That(result.Arguments, Does.Contain("--env-file"));
            Assert.That(result.Arguments, Does.Contain("/repo/database.env"));
            Assert.That(result.Arguments, Does.Contain("POSTGRES_PASSWORD=secret"));
            Assert.That(result.Arguments, Does.Contain("AGENT_UP_WORKSPACE_ID=workspace"));
            Assert.That(result.Arguments, Does.Contain("AGENT_UP_APPLICATION=db"));
        });
    }

    [Test]
    public void Host_rejects_a_blank_image()
    {
        var result = new DockerRuntimeCapability().Host(
            Request(new Dictionary<string, string> { ["image"] = "   " }));

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single(), Does.Contain("image"));
    }

    // Only a loopback URL is rewritten. Anything that is not a URL at all is passed through
    // unchanged rather than dropped, so a misconfiguration stays visible in the container.
    [Test]
    public void Host_keeps_an_audit_endpoint_that_is_not_a_url()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            auditEndpoint: "not-a-url"));

        Assert.That(result.Arguments, Does.Contain("AGENT_UP_AUDIT_ENDPOINT=not-a-url"));
    }

    [Test]
    public void Host_sends_no_audit_endpoint_when_the_Server_configured_none()
    {
        var result = new DockerRuntimeCapability().Host(
            Request(new Dictionary<string, string> { ["image"] = "postgres:17" }));

        Assert.That(result.Arguments.Any(argument => argument.StartsWith("AGENT_UP_AUDIT_ENDPOINT=", StringComparison.Ordinal)), Is.False);
    }

    // The image name ends the docker arguments: everything after it is the container's own
    // command, so an extra argument must not land among docker's own flags.
    [Test]
    public void Host_puts_the_container_command_after_the_image()
    {
        var result = new DockerRuntimeCapability().Host(Request(
            new Dictionary<string, string> { ["image"] = "postgres:17" },
            volumes: ["data:/var/lib/postgresql/data"],
            extra: ["postgres", "-c", "max_connections=200"]));

        var arguments = result.Arguments.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(arguments, Does.Contain("data:/var/lib/postgresql/data"));
            Assert.That(arguments[^3..], Is.EqualTo(new[] { "postgres", "-c", "max_connections=200" }));
            Assert.That(Array.IndexOf(arguments, "-v"), Is.LessThan(Array.IndexOf(arguments, "postgres:17")));
        });
    }

    [Test]
    public void Deliver_delivers_the_docker_nix_package_for_any_technology_version()
    {
        var delivered = new DockerRuntimeCapability().Deliver("28.0");

        Assert.Multiple(() =>
        {
            Assert.That(delivered.CanDeliver, Is.True);
            Assert.That(delivered.NixPackage, Is.EqualTo(DockerRuntimeCapability.NixPackage));
        });
    }

    [Test]
    public void Bind_requires_the_image_attribute_the_module_declares()
    {
        var bound = new DockerRuntimeCapability().Bind([Attributes("""{"name":"db"}""")]);

        Assert.That(bound.IsValid, Is.False);
        Assert.That(string.Join(" ", bound.Messages), Does.Contain("image"));
    }

    [Test]
    public void Bind_accepts_an_item_that_names_its_image()
    {
        var bound = new DockerRuntimeCapability().Bind([Attributes("""{"name":"db","image":"postgres:17"}""")]);

        Assert.That(bound.IsValid, Is.True);
    }

    // The package id is the module contract: the Server matches an agent-up.json section to a
    // module by this name, so a rename is a breaking change rather than a detail.
    [Test]
    public void The_module_publishes_the_identity_the_Server_matches_sections_by()
    {
        var capability = new DockerRuntimeCapability();

        Assert.Multiple(() =>
        {
            Assert.That(capability.SectionName, Is.EqualTo("docker"));
            Assert.That(capability.Identity.Id, Is.EqualTo("docker"));
            Assert.That(capability.Identity.PackageVersion, Is.EqualTo(DockerRuntimeCapability.PackageVersion));
            Assert.That(capability.NixPackages.Single().Package, Is.EqualTo(DockerRuntimeCapability.NixPackage));
            Assert.That(capability.ExtraAttributes.Select(attribute => attribute.Name), Does.Contain("image"));
        });
    }

    private static IReadOnlyDictionary<string, JsonElement> Attributes(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase);
    }

    private static RuntimeHostRequest Request(
        IReadOnlyDictionary<string, string>? parameters = null,
        IReadOnlyList<RuntimePortMapping>? ports = null,
        IReadOnlyList<string>? volumes = null,
        IReadOnlyList<string>? extra = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IReadOnlyList<string>? environmentFilePaths = null,
        string? auditEndpoint = null)
        => new(
            "db",
            null,
            parameters ?? new Dictionary<string, string>(),
            environment ?? new Dictionary<string, string>(),
            ports ?? [],
            volumes ?? [],
            extra ?? [],
            "workspace",
            "agentup-db",
            environmentFilePaths,
            auditEndpoint);
}
