using AgentUp.Desktop.Features.Capabilities.Controllers;
using AgentUp.Desktop.Features.Capabilities.Services;
using AgentUp.Desktop.Tests.Features.Capabilities.Unit;

namespace AgentUp.Desktop.Tests.Features.Capabilities.Controller;

[TestFixture]
public sealed class CapabilityModulesControllerTests
{
    [Test]
    public async Task ListAsync_delegatesToTheCatalogService()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [CapabilityModulesCatalogServiceTests.DotnetModule(true)]
        };
        var controller = new CapabilityModulesController(new CapabilityModulesCatalogService(client));

        var modules = await controller.ListAsync();

        Assert.That(modules.Single().DisplayName, Is.EqualTo(".NET"));
        Assert.That(client.ListCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task EnableAsync_mapsIdAndVersionOntoTheService()
    {
        var client = new FakeCapabilityModulesApiProvider();
        var controller = new CapabilityModulesController(new CapabilityModulesCatalogService(client));

        var module = await controller.EnableAsync("dotnet", "1.0.0");

        Assert.That(module.Enabled, Is.True);
        Assert.That(client.EnabledId, Is.EqualTo("dotnet"));
        Assert.That(client.EnabledVersion, Is.EqualTo("1.0.0"));
    }

    [Test]
    public async Task DisableAsync_mapsThePackageId()
    {
        var client = new FakeCapabilityModulesApiProvider();
        var controller = new CapabilityModulesController(new CapabilityModulesCatalogService(client));

        var module = await controller.DisableAsync("dotnet");

        Assert.That(module.Enabled, Is.False);
        Assert.That(client.DisabledId, Is.EqualTo("dotnet"));
    }
}
