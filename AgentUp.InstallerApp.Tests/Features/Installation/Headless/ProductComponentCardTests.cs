using AgentUp.InstallerApp.Features.Capabilities.Controllers;
using AgentUp.InstallerApp.Features.Capabilities.Models;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Headless;

[TestFixture]
public class ProductComponentCardTests
{
    private static ProductManifest EditorRenderer => new("Acme Studio", "acme-studio", "ACMESTUDIO")
    {
        Components =
        [
            new ProductComponent("editor", "Editor", "Visual editing surface."),
            new ProductComponent("renderer", "Renderer", "Output renderer.")
        ]
    };

    [Test]
    public void ComponentCards_matchManifestComponents_forTwoComponentProduct()
    {
        var manifest = EditorRenderer;
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/acme-studio",
            PayloadSelection.Bundled("Acme Studio", new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());

        Assert.That(model.ComponentCards.Select(c => c.Target.Id), Is.EqualTo(new[] { "editor", "renderer" }));
        Assert.That(model.ComponentCards.Select(c => c.Title), Is.EqualTo(new[] { "Editor", "Renderer" }));
        Assert.That(model.ComponentCards.Select(c => c.Description), Is.EqualTo(new[] { "Visual editing surface.", "Output renderer." }));
    }

    [Test]
    public void ComponentCards_noAgentUpDescriptionStrings_forNonAgentUpProduct()
    {
        var manifest = EditorRenderer;
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/acme-studio",
            PayloadSelection.Bundled("Acme Studio", new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());

        var allDescriptions = string.Join("|", model.ComponentCards.Select(c => c.Description));
        Assert.That(allDescriptions, Does.Not.Contain("Human UI for Agent-Up workspaces."));
        Assert.That(allDescriptions, Does.Not.Contain("Local runtime authority and API service."));
        Assert.That(allDescriptions, Does.Not.Contain("Terminal command wrapper for the local Server."));
    }

    [Test]
    public void ComponentCards_actionButtons_reflectInstallState_forNonAgentUpComponents()
    {
        var manifest = EditorRenderer;
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/acme-studio",
            PayloadSelection.Bundled("Acme Studio", new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());

        var editor = model.ComponentCards.Single(c => c.Target.Id == "editor");

        Assert.That(editor.PrimaryButtonText, Is.EqualTo("Install"));
        Assert.That(editor.StatusText, Is.EqualTo("Not installed"));
        Assert.That(editor.UpdateCommand.CanExecute(null), Is.False);
        Assert.That(editor.UninstallCommand.CanExecute(null), Is.False);
        Assert.That(editor.RepairCommand.CanExecute(null), Is.False);

        editor.ApplyStatus(new InstallerComponentStatus(editor.Target, InstallerComponentStatusKind.Installed, new Version(1, 0, 0), new Version(1, 0, 0)));

        Assert.That(editor.PrimaryButtonText, Is.EqualTo("Installed"));
        Assert.That(editor.StatusText, Is.EqualTo("Installed"));
        Assert.That(editor.InstallCommand.CanExecute(null), Is.False);
        Assert.That(editor.UpdateCommand.CanExecute(null), Is.False);
        Assert.That(editor.UninstallCommand.CanExecute(null), Is.True);
        Assert.That(editor.RepairCommand.CanExecute(null), Is.True);

        editor.ApplyStatus(new InstallerComponentStatus(editor.Target, InstallerComponentStatusKind.UpdateAvailable, new Version(1, 0, 0), new Version(1, 1, 0)));

        Assert.That(editor.PrimaryButtonText, Is.EqualTo("Update"));
        Assert.That(editor.InstallCommand.CanExecute(null), Is.True);
        Assert.That(editor.UpdateCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void ComponentCards_matchManifestComponents_forAgentUpThreeComponentProduct()
    {
        var manifest = ProductManifest.AgentUp();
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());

        Assert.That(model.ComponentCards, Has.Count.EqualTo(3));
        Assert.That(model.ComponentCards.Select(c => c.Target.Id), Is.EqualTo(new[] { "desktop", "server", "cli" }));
    }

    [Test]
    public void CapabilityCard_withoutMatchingActiveVersion_doesNotShowActiveVersionDetail()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(), new Version(1, 0, 0), "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());
        var module = new InstalledCapabilityModule(
            "dotnet",
            ".NET",
            ".NET SDK capability.",
            "10.0.x",
            [new CapabilityInstalledVersion("dotnet", "9.0.x", "/tool-cache/dotnet/9.0.x", CapabilityVersionSource.AgentUpManaged, true)]);

        var card = new CapabilityCardViewModel(module, model);

        Assert.That(card.ActiveVersion, Is.Empty);
        Assert.That(card.Detail, Is.EqualTo("No active version selected"));
        Assert.That(card.Versions.Single().ActiveText, Is.EqualTo("Available"));
    }

    [Test]
    public async Task InstallCatalogModuleAsync_forExistingCapability_reusesCardAndAppliesInstalledModule()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(), new Version(1, 0, 0), "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());
        var existingModule = new InstalledCapabilityModule(
            "dotnet",
            ".NET",
            ".NET SDK capability.",
            "9.0.x",
            [new CapabilityInstalledVersion("dotnet", "9.0.x", "/tool-cache/dotnet/9.0.x", CapabilityVersionSource.AgentUpManaged, true)]);
        var existingCard = new CapabilityCardViewModel(existingModule, model);
        model.CapabilityCards.Add(existingCard);
        var catalogEntry = new CatalogCapabilityViewModel(
            new CapabilityCatalogEntry(
                "dotnet",
                ".NET",
                ".NET SDK capability.",
                [new CapabilityArtifact("dotnet", "10.0.x", new Uri("https://example.invalid/dotnet.tar.gz"), "abc123")]),
            isInstalled: true,
            supportsInstallActions: true,
            model);

        await model.InstallCatalogModuleAsync(catalogEntry);

        Assert.That(model.CapabilityCards, Has.Count.EqualTo(1));
        Assert.That(model.CapabilityCards.Single(), Is.SameAs(existingCard));
        Assert.That(existingCard.StatusText, Is.EqualTo("Installed"));
        Assert.That(existingCard.ActiveVersion, Is.EqualTo("10.0.x"));
    }

    [Test]
    public void CatalogCapabilityButtonText_forNixOsManagedInstalledCapability_reportsManagedByNixOs()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(), new Version(1, 0, 0), "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            CapabilitiesController.CreateFake());
        var catalogEntry = new CatalogCapabilityViewModel(
            new CapabilityCatalogEntry(
                "dotnet",
                ".NET",
                ".NET SDK capability.",
                [new CapabilityArtifact("dotnet", "10.0.x", new Uri("https://example.invalid/dotnet.tar.gz"), "abc123")]),
            isInstalled: true,
            supportsInstallActions: false,
            model);

        Assert.That(catalogEntry.ButtonText, Is.EqualTo("Managed by NixOS"));
        Assert.That(catalogEntry.InstallCommand.CanExecute(null), Is.False);
    }
}
