using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Processes.Providers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class DockerProcessProviderCapabilityTests
{
    [Test]
    public void CreateRunArguments_hosts_through_the_enabled_docker_runtime()
    {
        var runtime = new StubRuntimeCapability { Identity = new("docker", "1.0.0", "Docker", "agent-up") };
        var controller = new CapabilityModulesController(CapabilityModuleHarness.CreateService(
            enabled: true,
            manifest: FakeEnabledCapabilityPackages.Docker(),
            loader: new FixedCapabilityModuleLoader(new CapabilityLoadedModule(runtime, null))));
        var app = new ApplicationInstance
        {
            Name = "db",
            ServiceType = ServiceType.Docker,
            Image = "postgres:17",
            CapabilityId = "docker"
        };
        var workspace = new Workspace
        {
            Id = "workspace",
            DisplayName = "ws",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications = [app]
        };

        var args = new DockerProcessProvider(capabilities: controller).CreateRunArguments("agentup-db", workspace, app);

        Assert.That(args, Is.EqualTo(new[] { "run", "-d", "--name", "agentup-db", "postgres:17" }));
    }
}
