using AgentUp.Desktop.Features.Capabilities.Controllers;
using AgentUp.Desktop.Features.Capabilities.Services;
using AgentUp.Desktop.Features.Capabilities.ViewModels;
using AgentUp.Desktop.Tests.Features.Capabilities.Unit;

namespace AgentUp.Desktop.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityModulesViewModelTests
{
    [Test]
    public async Task OpenAsync_loadsCardsFromTheServerCatalog()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [CapabilityModulesCatalogServiceTests.DotnetModule(true)]
        };
        var viewModel = new CapabilityModulesViewModel(
            new CapabilityModulesController(new CapabilityModulesCatalogService(client)));

        await viewModel.OpenAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsOpen, Is.True);
            Assert.That(viewModel.Modules.Single().DisplayName, Is.EqualTo(".NET"));
            Assert.That(viewModel.Status, Is.Null);
        });
    }

    [Test]
    public async Task EnableAsync_reloadsTheCatalogAfterTheServerEnable()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [CapabilityModulesCatalogServiceTests.DotnetModule(false)]
        };
        var viewModel = new CapabilityModulesViewModel(
            new CapabilityModulesController(new CapabilityModulesCatalogService(client)));

        await viewModel.EnableAsync("dotnet", "1.0.0");

        Assert.That(client.EnabledId, Is.EqualTo("dotnet"));
        Assert.That(client.ListCalls, Is.GreaterThanOrEqualTo(1));
    }
}
