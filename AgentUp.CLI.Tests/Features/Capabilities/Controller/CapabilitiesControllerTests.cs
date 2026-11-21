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
            Assert.That(output.ToString(), Does.Contain($"Capability '{CliDomain.UnknownCapabilityId}' is not built into this Server."));
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
    public async Task Enable_enablesABuiltInCapability()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi();

        var code = await Controller(api, output).RunAsync(["enable", CliDomain.CapabilityId]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(output.ToString(), Does.Contain($"Enabled {CliDomain.CapabilityId} {CliDomain.CapabilityVersion}."));
            Assert.That(api.EnabledId, Is.EqualTo(CliDomain.CapabilityId));
        });
    }

    [Test]
    public async Task Bootstrap_enablesEveryCapabilityWhoseSoftwareIsFound()
    {
        using var output = new StringWriter();
        var api = new FakeCapabilityModulesApi
        {
            Modules =
            [
                CliDomain.Capability().Named("dotnet").Build(),
                CliDomain.Capability().Named("opencode").Build(),
                CliDomain.Capability().Named("cursor").Missing("agent").Build(),
                CliDomain.Capability().Named("github").Build()
            ]
        };

        var code = await Controller(api, output).RunAsync(["bootstrap"]);

        Assert.Multiple(() =>
        {
            Assert.That(code, Is.Zero);
            Assert.That(api.EnabledIds, Is.EqualTo(new[] { "dotnet", "opencode", "github" }));
            Assert.That(output.ToString(), Does.Contain("Skipped cursor: agent not found."));
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
        TextWriter output)
    {
        var service = new CapabilitiesService(api, new CapabilitiesArgParser());
        var formatted = new CapabilitiesOutputService(output);
        return new CapabilitiesController(
            new CapabilitiesListCommand(service, formatted),
            new CapabilitiesEnableCommand(service, formatted),
            new CapabilitiesDisableCommand(service, formatted),
            new CapabilitiesBootstrapCommand(service, formatted),
            formatted);
    }
}
