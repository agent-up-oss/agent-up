using LocalInstaller.App.Features.Capabilities.Controllers;
using LocalInstaller.App.Features.Capabilities.Factories;
using LocalInstaller.App.Features.Capabilities.Models;
using LocalInstaller.App.Features.Installation.ViewModels;
using LocalInstaller.App.Tests.Support;
using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.Installation.Providers;
using LocalInstaller.Core.Features.PrerequisiteChecks.Models;

namespace LocalInstaller.App.Tests.Features.Installation.Headless;

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
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));

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
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));

        var allDescriptions = string.Join("|", model.ComponentCards.Select(c => c.Description));
        Assert.That(allDescriptions, Does.Not.Contain("Human UI for Agent-Up workspaces."));
        Assert.That(allDescriptions, Does.Not.Contain("Local runtime authority, API service, and tray app."));
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
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));

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
    public void ComponentCards_matchManifestComponents_forAgentUpProduct()
    {
        var manifest = AgentUpInstallerAppTestManifests.Product();
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), "/opt/agent-up",
            AgentUpInstallerAppTestManifests.BundledPayload(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));

        Assert.That(model.ComponentCards, Has.Count.EqualTo(4));
        Assert.That(model.ComponentCards.Select(c => c.Target.Id), Is.EqualTo(new[] { "desktop", "server", "cli", "tray" }));
    }

    [Test]
    public async Task RefreshCommand_rechecksComponentStatusAndUpdatesPrimaryButton()
    {
        var session = InstallerSession.CreateDefault(
            AgentUpInstallerAppTestManifests.Product(), new Version(1, 0, 0), "/opt/agent-up",
            AgentUpInstallerAppTestManifests.BundledPayload(new Version(1, 0, 0)));
        var adapter = new RefreshStatusAdapter();
        var model = new InstallerViewModel(
            session,
            adapter,
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateEmpty()));
        var desktop = model.ComponentCards.Single(c => c.Target.Id == "desktop");

        await model.RefreshAsync();
        Assert.That(desktop.PrimaryButtonText, Is.EqualTo("Installed"));

        adapter.HasUpdate = true;
        model.RefreshCommand.Execute(null);
        await WaitUntilAsync(() => desktop.PrimaryButtonText == "Update");

        Assert.That(desktop.StatusText, Is.EqualTo("Update available"));
        Assert.That(desktop.InstallCommand.CanExecute(null), Is.True);
        Assert.That(desktop.UpdateCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void CapabilityCard_withoutMatchingActiveVersion_doesNotShowActiveVersionDetail()
    {
        var session = InstallerSession.CreateDefault(
            AgentUpInstallerAppTestManifests.Product(), new Version(1, 0, 0), "/opt/agent-up",
            AgentUpInstallerAppTestManifests.BundledPayload(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));
        var module = new InstalledCapabilityModule(
            "dotnet",
            ".NET",
            ".NET SDK capability.",
            "10.0.x",
            [new CapabilityInstalledVersion("dotnet", "9.0.x", "/tool-cache/dotnet/9.0.x", CapabilityVersionSource.Managed, true)]);

        var card = new CapabilityCardViewModel(module, model);

        Assert.That(card.ActiveVersion, Is.Empty);
        Assert.That(card.Detail, Is.EqualTo("No active version selected"));
        Assert.That(card.Versions.Single().ActiveText, Is.EqualTo("Available"));
    }

    [Test]
    public async Task InstallCatalogModuleAsync_forExistingCapability_reusesCardAndAppliesInstalledModule()
    {
        var session = InstallerSession.CreateDefault(
            AgentUpInstallerAppTestManifests.Product(), new Version(1, 0, 0), "/opt/agent-up",
            AgentUpInstallerAppTestManifests.BundledPayload(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));
        var existingModule = new InstalledCapabilityModule(
            "dotnet",
            ".NET",
            ".NET SDK capability.",
            "9.0.x",
            [new CapabilityInstalledVersion("dotnet", "9.0.x", "/tool-cache/dotnet/9.0.x", CapabilityVersionSource.Managed, true)]);
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
            AgentUpInstallerAppTestManifests.Product(), new Version(1, 0, 0), "/opt/agent-up",
            AgentUpInstallerAppTestManifests.BundledPayload(new Version(1, 0, 0)));
        var model = new InstallerViewModel(
            session,
            new FakeInstallerPlatformAdapter(),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        Assert.Fail("Condition was not met.");
    }

    private sealed class RefreshStatusAdapter : IInstallerPlatformAdapter
    {
        public bool HasUpdate { get; set; }

        public string PlatformName => "Test";

        public bool SupportsInstallActions => true;

        public Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new DockerStatus(DockerStatusKind.Operational, "Docker is operational", "Test Docker."));

        public Task<InstallerComponentStatus> GetComponentStatusAsync(
            ProductComponent component,
            InstallerSession session,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new InstallerComponentStatus(
                component,
                HasUpdate ? InstallerComponentStatusKind.UpdateAvailable : InstallerComponentStatusKind.Installed,
                session.Version,
                HasUpdate ? new Version(1, 1, 0) : session.Version));

        public IReadOnlyList<InstallOperation> PlanComponentAction(
            ProductComponent component,
            InstallerComponentAction action,
            InstallerSession session)
            => [];

        public async IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
            ProductComponent component,
            InstallerComponentAction action,
            InstallerSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
            => [];

        public async IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
            InstallerSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ValidationReport> ValidateInstalledStateAsync(
            InstallerSession session,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidationReport([]));
    }
}
