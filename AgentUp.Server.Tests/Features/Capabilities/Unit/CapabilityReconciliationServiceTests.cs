using System.Text.Json;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityReconciliationServiceTests
{
    [Test]
    public async Task Missing_dotnet_adapter_returns_an_unrunnable_application_with_declared_metadata()
    {
        var service = new CapabilityReconciliationService();
        var ports = new[] { ServerDomain.Port().Named("WEB_PORT").On(5000).Build() };
        var allocated = new[] { new PortMapping("WEB_PORT", 5000, 12000, "http") };

        var result = await service.ReconcileDotnetAsync(
            new DotnetApplicationDefinition("api", "10.0", new DotnetRunDefinition("Api.csproj"),
                Environment: new Dictionary<string, string> { ["MODE"] = "test" }), ports, allocated);

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("api"));
            Assert.That(result.Command, Is.Empty);
            Assert.That(result.CapabilityId, Is.EqualTo("dotnet"));
            Assert.That(result.CapabilityVersionRequirement, Is.EqualTo("10.0"));
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.Ports, Is.SameAs(ports));
            Assert.That(result.AllocatedPorts, Is.SameAs(allocated));
            Assert.That(result.Environment!["MODE"], Is.EqualTo("test"));
        });
    }

    [Test]
    public async Task Missing_docker_adapter_preserves_image_volume_command_and_database_settings()
    {
        var result = await new CapabilityReconciliationService().ReconcileDockerAsync(
            new DockerCapabilityDefinition("db", "postgres:17", Volumes: ["data:/var/lib/postgresql/data"],
                Command: ["postgres", "-c", "log_statement=all"], Database: true), [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("db"));
            Assert.That(result.Image, Is.EqualTo("postgres:17"));
            Assert.That(result.Volumes, Is.EqualTo(new[] { "data:/var/lib/postgresql/data" }));
            Assert.That(result.Args, Is.EqualTo(new[] { "postgres", "-c", "log_statement=all" }));
            Assert.That(result.Database, Is.True);
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.CapabilityStatus.Messages.Single(), Does.Contain("not enabled"));
        });
    }

    [Test]
    public async Task Enabled_dotnet_package_maps_agent_up_json_project_onto_the_launch_template()
    {
        var packages = new EnabledPackages(CapabilityModuleHarness.DotnetManifest());
        var templates = new AgentUp.Registry.Features.Packages.Controllers.CapabilityPackageController(
            new AgentUp.Registry.Features.Packages.Services.CapabilityPackageValidator(),
            new AgentUp.Registry.Features.Packages.Services.CapabilityTemplateRenderer());
        var result = await new CapabilityReconciliationService(packages, templates).ReconcileDotnetAsync(
            new DotnetApplicationDefinition("api", "10.0", new DotnetRunDefinition("Api.csproj")), [], []);

        Assert.That(result.CapabilityStatus!.CanRun, Is.True);
        Assert.That(result.Command, Does.Contain("dotnet"));
        Assert.That(result.Command, Does.Contain("Api.csproj"));
    }

    [Test]
    public async Task Enabled_runtime_module_hosts_dotnet_from_the_sdk_contract()
    {
        var runtime = new StubRuntimeCapability
        {
            ExtraAttributes =
            [
                new RuntimeAttributeSpec("project", true, "path"),
                new RuntimeAttributeSpec("run", false, "object"),
                new RuntimeAttributeSpec("arguments", false, "string")
            ]
        };
        var packages = new EnabledPackages(CapabilityModuleHarness.DotnetManifest(), runtime);
        var result = await new CapabilityReconciliationService(packages).ReconcileDotnetAsync(
            new DotnetApplicationDefinition("api", "10.0.x", new DotnetRunDefinition("Api.csproj", ["--no-launch-profile"])), [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.CapabilityStatus!.CanRun, Is.True);
            Assert.That(result.LaunchFileName, Is.EqualTo("dotnet"));
            Assert.That(result.LaunchArguments, Is.EqualTo(new[] { "run", "--project", "Api.csproj", "--no-launch-profile" }));
            Assert.That(result.Command, Does.Contain("Api.csproj"));
        });
    }

    [Test]
    public async Task Enabled_runtime_module_delivers_docker_without_hosting_at_reconcile()
    {
        var runtime = new StubRuntimeCapability
        {
            Identity = new("docker", "1.0.0", "Docker", "agent-up"),
            ExtraAttributes =
            [
                new RuntimeAttributeSpec("image", true, "string"),
                new RuntimeAttributeSpec("volumes", false, "array"),
                new RuntimeAttributeSpec("command", false, "array")
            ]
        };
        var packages = new EnabledPackages(FakeEnabledCapabilityPackages.Docker(), runtime);
        var result = await new CapabilityReconciliationService(packages).ReconcileDockerAsync(
            new DockerCapabilityDefinition("db", "postgres:17"), [], []);

        Assert.That(result.CapabilityStatus!.CanRun, Is.True);
        Assert.That(result.LaunchFileName, Is.Null);
        Assert.That(result.Image, Is.EqualTo("postgres:17"));
    }

    [Test]
    public async Task Enabled_runtime_module_hosts_an_unknown_section_from_bound_items()
    {
        var runtime = new StubRuntimeCapability
        {
            Identity = new("python", "1.0.0", "Python", "agent-up"),
            ExtraAttributes = [new RuntimeAttributeSpec("script", true, "path")]
        };
        var packages = new EnabledPackages(new CapabilityPackageManifest
        {
            Id = "python",
            Version = "1.0.0",
            Kind = "runtime"
        }, runtime);
        var item = new RuntimeSectionItem(
            "api",
            Parameters: new Dictionary<string, string> { ["script"] = "main.py" },
            Attributes: new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("api"),
                ["script"] = JsonSerializer.SerializeToElement("main.py")
            });

        var result = await new CapabilityReconciliationService(packages).ReconcileRuntimeAsync("python", item, [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.CapabilityId, Is.EqualTo("python"));
            Assert.That(result.CapabilityStatus!.CanRun, Is.True);
            Assert.That(result.LaunchFileName, Is.Not.WhiteSpace);
        });
    }

    // Binding is what turns a named section into something runnable, so a section the module
    // rejects has to surface the module's own words rather than a launch nobody can run.
    [Test]
    public async Task Section_the_module_rejects_is_unrunnable_and_keeps_the_binder_messages()
    {
        var runtime = new StubRuntimeCapability
        {
            Identity = new("python", "1.0.0", "Python", "agent-up"),
            ExtraAttributes = [new RuntimeAttributeSpec("script", true, "path")]
        };
        var packages = new EnabledPackages(PythonManifest(), runtime);
        var item = new RuntimeSectionItem(
            "worker",
            Path: "services/worker",
            Attributes: new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("worker"),
                ["nonsense"] = JsonSerializer.SerializeToElement("value")
            });

        var result = await new CapabilityReconciliationService(packages).ReconcileRuntimeAsync("python", item, [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.CapabilityStatus.Messages, Has.Some.Contains("script"));
            Assert.That(result.CapabilityStatus.Messages, Has.Some.Contains("nonsense"));
            Assert.That(result.Name, Is.EqualTo("worker"));
            Assert.That(result.Path, Is.EqualTo("services/worker"));
            Assert.That(result.ServiceType, Is.EqualTo(ServiceType.Process));
        });
    }

    [Test]
    public async Task Runtime_module_that_cannot_deliver_reports_why_instead_of_a_launch()
    {
        var runtime = new StubRuntimeCapability
        {
            Identity = new("python", "1.0.0", "Python", "agent-up"),
            ExtraAttributes = [new RuntimeAttributeSpec("script", true, "path")],
            CanDeliver = false
        };
        var packages = new EnabledPackages(PythonManifest(), runtime);
        var item = new RuntimeSectionItem(
            "worker",
            Attributes: new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("worker"),
                ["script"] = JsonSerializer.SerializeToElement("main.py")
            });

        var result = await new CapabilityReconciliationService(packages).ReconcileRuntimeAsync("python", item, [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.CapabilityStatus.Messages, Is.Not.Empty);
            Assert.That(result.LaunchFileName, Is.Null.Or.Empty);
        });
    }

    // A container-shaped section is delivered but not hosted here, so a module that cannot
    // deliver the requested technology version has to say so on the instance.
    [Test]
    public async Task Docker_section_reports_a_technology_version_the_module_cannot_deliver()
    {
        var runtime = new StubRuntimeCapability
        {
            Identity = new("docker", "1.0.0", "Docker", "agent-up"),
            ExtraAttributes = [new RuntimeAttributeSpec("image", true, "string")],
            CanDeliver = false
        };
        var packages = new EnabledPackages(FakeEnabledCapabilityPackages.Docker(), runtime);

        var result = await new CapabilityReconciliationService(packages).ReconcileDockerAsync(
            new DockerCapabilityDefinition("db", "postgres:17"), [], []);

        Assert.Multiple(() =>
        {
            Assert.That(result.ServiceType, Is.EqualTo(ServiceType.Docker));
            Assert.That(result.CapabilityStatus!.CanRun, Is.False);
            Assert.That(result.CapabilityStatus.Messages, Is.Not.Empty);
        });
    }

    private static CapabilityPackageManifest PythonManifest()
        => new()
        {
            Id = "python",
            Version = "1.0.0",
            Kind = "runtime"
        };

    private sealed class EnabledPackages(
        CapabilityPackageManifest manifest,
        IRuntimeCapability? runtime = null) : IEnabledCapabilityPackages
    {
        public CapabilityPackageManifest? GetEnabled(string id)
            => manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? manifest : null;

        public CapabilityLaunchPlan? AgentLaunch(string id) => null;

        public CapabilityLaunchWrapDto WrapLaunch(string fileName, IReadOnlyList<string> arguments)
            => new(fileName, arguments);

        public CapabilityLaunchWrapDto WrapModule(string id, string fileName, IReadOnlyList<string> arguments)
            => new(fileName, arguments);

        public IRuntimeCapability? GetRuntime(string id)
            => runtime is not null && runtime.Identity.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ? runtime : null;

        public IReadOnlyList<IRuntimeCapability> ListRuntimes()
            => runtime is null ? [] : [runtime];

        public IAgentCapability? GetAgent(string id) => null;

        public IReadOnlyList<IAgentCapability> ListAgents() => [];
    }
}
