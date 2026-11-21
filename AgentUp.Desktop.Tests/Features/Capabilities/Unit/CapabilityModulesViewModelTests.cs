using System.Net.Http;
using AgentUp.Desktop.Features.Capabilities.Controllers;
using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;
using AgentUp.Desktop.Features.Capabilities.Services;
using AgentUp.Desktop.Features.Capabilities.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Features.Capabilities.Unit;
using AgentUp.Desktop.Tests.Support;
using System.Reactive.Linq;

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

        await viewModel.EnableAsync("dotnet");

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

    // An empty list only happens against a Server that predates built-in capabilities, so it has
    // to read as an explanation rather than an empty panel.
    [Test]
    public async Task OpenAsync_saysSoWhenTheServerListsNoCapabilities()
    {
        var viewModel = CreateViewModel(new FakeCapabilityModulesApiProvider());

        await viewModel.OpenAsync();

        Assert.That(viewModel.Modules, Is.Empty);
        Assert.That(viewModel.Status, Is.EqualTo("The Server reports no capabilities."));
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
    public async Task Cards_exposeStableEnableAndDisableAutomationIds()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [CapabilityModulesCatalogServiceTests.DotnetModule(true)]
        };
        var viewModel = CreateViewModel(client);

        await viewModel.OpenAsync();

        var card = viewModel.Modules.Single();
        Assert.That(card.EnableAutomationId, Is.EqualTo(DesktopAutomationIds.CapabilityEnable("dotnet")));
        Assert.That(card.DisableAutomationId, Is.EqualTo(DesktopAutomationIds.CapabilityDisable("dotnet")));
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
            Assert.That(disabled.StatusLabel, Is.EqualTo("Off"));
            Assert.That(disabled.Summary, Is.EqualTo("not enabled"));
            Assert.That(disabled.CanRun, Is.False);
        });
    }

    [Test]
    public async Task Cards_keepTheNixInstallUrlInTheTooltipWhenTheServerSaysNixIsMissing()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules =
            [
                DesktopDomain.Capability().Missing("nix").Enabled().Build()
            ]
        };
        var viewModel = CreateViewModel(client);

        await viewModel.OpenAsync();

        var card = viewModel.Modules.Single();
        Assert.Multiple(() =>
        {
            Assert.That(card.Summary, Does.Contain("https://nixos.org/download"));
            Assert.That(card.Summary, Does.Not.Contain("is not enabled"));
            Assert.That(card.CanRun, Is.False);
        });
    }

    [Test]
    public async Task A_capability_whose_software_is_missing_cannot_be_enabled_and_says_so_in_the_button()
    {
        var viewModel = CreateViewModel(new FakeCapabilityModulesApiProvider
        {
            Modules =
            [
                DesktopDomain.Capability().Missing("nix").Build(),
                DesktopDomain.Capability().Named("cursor", "Cursor", "agent").Missing("agent").Build()
            ]
        });

        await viewModel.OpenAsync();

        var dotnet = viewModel.Modules.Single(card => card.Id == "dotnet");
        var cursor = viewModel.Modules.Single(card => card.Id == "cursor");
        Assert.Multiple(() =>
        {
            Assert.That(dotnet.EnableLabel, Is.EqualTo("Needs Nix"));
            Assert.That(cursor.EnableLabel, Is.EqualTo("Not found"));
            Assert.That(cursor.StatusLabel, Is.EqualTo("Missing agent"));
            Assert.That(cursor.IsUnavailable && !cursor.CanEnable, Is.True);
            Assert.That(dotnet.EnableCommand.CanExecute.FirstAsync().Wait(), Is.False);
        });
    }

    [Test]
    public async Task Settings_expand_only_once_the_capability_is_enabled()
    {
        var viewModel = CreateViewModel(new FakeCapabilityModulesApiProvider
        {
            Modules =
            [
                DesktopDomain.Capability().Named("opencode", "OpenCode", "agent").WithSetting("endpoint", "url").Build(),
                DesktopDomain.Capability().Named("claude", "Claude", "agent").WithSetting("endpoint", "url").Enabled().NeedsSetup().Build()
            ]
        });

        await viewModel.OpenAsync();

        Assert.That(viewModel.Modules.Single(card => card.Id == "opencode").ShowsSettings, Is.False);
        Assert.That(viewModel.Modules.Single(card => card.Id == "claude").ShowsSettings, Is.True);
        Assert.That(viewModel.Modules.Single(card => card.Id == "claude").StatusLabel, Is.EqualTo("Needs setup"));
    }

    [Test]
    public async Task Saving_settings_sends_changed_values_and_leaves_an_untouched_secret_out()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules =
            [
                DesktopDomain.Capability().Named("opencode", "OpenCode", "agent")
                    .WithSetting("endpoint", "url", "http://old.test")
                    .WithSetting("token", "secret", hasValue: true)
                    .WithSetting("model", "text", "qwen")
                    .Enabled()
                    .Build()
            ]
        };
        var viewModel = CreateViewModel(client);
        await viewModel.OpenAsync();
        var card = viewModel.Modules.Single();
        card.Settings.Single(field => field.Key == "endpoint").Value = "http://new.test";

        await card.SaveSettingsCommand.Execute();

        Assert.That(client.SavedValues, Is.EqualTo(new Dictionary<string, string> { ["endpoint"] = "http://new.test" }));
        Assert.That(card.Settings.Single(field => field.Key == "token").Placeholder, Is.EqualTo("Stored"));
        Assert.That(card.Settings.Single(field => field.Key == "token").PasswordChar, Is.Not.EqualTo('\0'));
    }

    private static CapabilityModulesViewModel CreateViewModel(ICapabilityModulesApiProvider client)
        => new(new CapabilityModulesController(new CapabilityModulesCatalogService(client)));
}

internal sealed class UnreachableCapabilityModulesApiProvider : ICapabilityModulesApiProvider
{
    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");

    public Task<CapabilityModuleDto> EnableAsync(string id, CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");

    public Task<CapabilityModuleDto> UpdateSettingsAsync(
        string id,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
        => throw new HttpRequestException("Server unreachable");
}
