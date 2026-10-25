using AgentUp.CLI.Features.Capabilities.Controllers;
using AgentUp.CLI.Features.Capabilities.Providers;
using AgentUp.CLI.Features.Capabilities.Services;
using AgentUp.CLI.Tests.Fake;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilitiesControllerTests
{
    [Test]
    public async Task List_printsWhyAModuleCannotRun()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi
        {
            Modules = [CliDomain.Capability().UnableToRun("Capability 'dotnet' is not enabled.", "Run: agent-up capabilities bootstrap").Build()]
        };

        var code = await Controller(api, output).RunAsync(["list"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("dotnet"));
            Assert.That(output.ToString(), Does.Contain("canRun=false"));
            Assert.That(output.ToString(), Does.Contain("agent-up capabilities bootstrap"));
        });
    }

    [Test]
    public async Task Enable_rejectsAnUnknownId()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi();

        var code = await Controller(api, output).RunAsync(["enable", CliDomain.UnknownCapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain($"Capability '{CliDomain.UnknownCapabilityId}' is not in the local registry."));
            Assert.That(api.EnabledId, Is.Null);
        });
    }

    [Test]
    public async Task Enable_reportsWhenTheModuleIsAlreadyEnabled()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi { Modules = [CliDomain.Capability().Enabled().Build()] };

        var code = await Controller(api, output).RunAsync(["enable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("already enabled"));
            Assert.That(api.EnabledId, Is.Null);
        });
    }

    [Test]
    public async Task Disable_disablesAnEnabledModule()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi { Modules = [CliDomain.Capability().Enabled().Build()] };

        var code = await Controller(api, output).RunAsync(["disable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain($"Disabled {CliDomain.CapabilityId}."));
            Assert.That(api.DisabledId, Is.EqualTo(CliDomain.CapabilityId));
        });
    }

    [Test]
    public async Task Enable_enablesAPackedModule()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi();

        var code = await Controller(api, output).RunAsync(["enable", CliDomain.CapabilityId, "--version", CliDomain.CapabilityVersion]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain($"Enabled {CliDomain.CapabilityId} {CliDomain.CapabilityVersion}."));
            Assert.That(api.EnabledId, Is.EqualTo(CliDomain.CapabilityId));
            Assert.That(api.EnabledVersion, Is.EqualTo(CliDomain.CapabilityVersion));
        });
    }

    [Test]
    public async Task Bootstrap_packsThenEnablesTheFirstPartySet()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi
        {
            Modules =
            [
                CliDomain.Capability().Named("dotnet").Build(),
                CliDomain.Capability().Named("docker").Build(),
                CliDomain.Capability().Named("codex").Build(),
                CliDomain.Capability().Named("cursor").Build(),
                CliDomain.Capability().Named("claude").Build(),
                CliDomain.Capability().Named("github").Build(),
                CliDomain.Capability().Named("forgejo").Build()
            ]
        };
        var packer = new FakeCapabilityPacker();

        var code = await Controller(api, output, packer).RunAsync(["bootstrap"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(packer.Calls, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Packed first-party capabilities"));
            Assert.That(output.ToString(), Does.Contain("Enabled github"));
            Assert.That(output.ToString(), Does.Contain("Enabled forgejo"));
        });
    }

    [Test]
    public async Task Help_listsTheVerbs()
    {
        using var output = new StringWriter();

        var code = await Controller(new FakeCapabilityModulesApi(), output).RunAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("list"));
            Assert.That(output.ToString(), Does.Contain("bootstrap"));
        });
    }

    private static CapabilitiesController Controller(
        FakeCapabilityModulesApi api,
        TextWriter output,
        FakeCapabilityPacker? packer = null)
    {
        var service = new CapabilitiesService(api, packer ?? new FakeCapabilityPacker(), new CapabilitiesArgParser());
        var formatted = new CapabilitiesOutputService(output);
        const string workingDirectory = "/repo";
        return new CapabilitiesController(
            new CapabilitiesListCommand(service, formatted, workingDirectory),
            new CapabilitiesEnableCommand(service, formatted, workingDirectory),
            new CapabilitiesDisableCommand(service, formatted, workingDirectory),
            new CapabilitiesBootstrapCommand(service, formatted, workingDirectory),
            formatted);
    }
}
