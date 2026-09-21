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
