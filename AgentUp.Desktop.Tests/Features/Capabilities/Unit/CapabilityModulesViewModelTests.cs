using System.Net.Http;
using AgentUp.Desktop.Features.Capabilities.Controllers;
using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;
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

    [Test]
    public async Task DisableAsync_reloadsTheCatalogAfterTheServerDisable()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [CapabilityModulesCatalogServiceTests.DotnetModule(true)]
        };
        var viewModel = CreateViewModel(client);

        await viewModel.DisableAsync("dotnet");

        Assert.That(client.DisabledId, Is.EqualTo("dotnet"));
        Assert.That(client.ListCalls, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Close_hidesTheCatalogWithoutDiscardingIt()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [CapabilityModulesCatalogServiceTests.DotnetModule(true)]
        };
        var viewModel = CreateViewModel(client);
        await viewModel.OpenAsync();

        viewModel.Close();

        Assert.That(viewModel.IsOpen, Is.False);
        Assert.That(viewModel.Modules, Is.Not.Empty);
    }

    // An empty registry is the state a Server that has never packed a capability is in, so it
    // has to read as an explanation rather than an empty panel.
    [Test]
    public async Task OpenAsync_saysSoWhenTheServerRegistryIsEmpty()
    {
        var viewModel = CreateViewModel(new FakeCapabilityModulesApiProvider());

        await viewModel.OpenAsync();

        Assert.That(viewModel.Modules, Is.Empty);
        Assert.That(viewModel.Status, Is.EqualTo("No capability modules in the Server registry."));
    }

    [Test]
    public async Task OpenAsync_reportsAServerThatCannotBeReached()
    {
        var viewModel = CreateViewModel(new UnreachableCapabilityModulesApiProvider());

        await viewModel.OpenAsync();

        Assert.That(viewModel.IsOpen, Is.True);
        Assert.That(viewModel.Status, Is.EqualTo("Server unreachable"));
    }

    [Test]
    public async Task Cards_carryTheServerVerdictForEachModule()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules =
            [
                CapabilityModulesCatalogServiceTests.DotnetModule(true),
                CapabilityModulesCatalogServiceTests.DotnetModule(false) with { Id = "docker" }
            ]
        };
        var viewModel = CreateViewModel(client);

        await viewModel.OpenAsync();

        var ready = viewModel.Modules.Single(card => card.Id == "dotnet");
        var disabled = viewModel.Modules.Single(card => card.Id == "docker");
        Assert.Multiple(() =>
        {
            Assert.That(ready.StatusLabel, Is.EqualTo("Ready"));
            Assert.That(ready.Summary, Is.Null);
            Assert.That(ready.Publisher, Is.EqualTo("agent-up"));
            Assert.That(ready.Version, Is.EqualTo("1.0.0"));
            Assert.That(ready.Enabled, Is.True);
            Assert.That(disabled.StatusLabel, Is.EqualTo("disabled"));
            Assert.That(disabled.Summary, Is.EqualTo("not enabled"));
            Assert.That(disabled.CanRun, Is.False);
        });
    }

    private static CapabilityModulesViewModel CreateViewModel(ICapabilityModulesApiProvider client)
        => new(new CapabilityModulesController(new CapabilityModulesCatalogService(client)));
}

internal sealed class UnreachableCapabilityModulesApiProvider : ICapabilityModulesApiProvider
{
    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");

    public Task<CapabilityModuleDto> EnableAsync(string id, string? version, CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");
}
