using AgentUp.InstallerApp.Features.Capabilities.Controllers;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Installation;

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

        Assert.That(editor.PrimaryButtonText, Is.EqualTo("Update"));
        Assert.That(editor.StatusText, Is.EqualTo("Installed"));
        Assert.That(editor.UpdateCommand.CanExecute(null), Is.True);
        Assert.That(editor.UninstallCommand.CanExecute(null), Is.True);
        Assert.That(editor.RepairCommand.CanExecute(null), Is.True);
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
}
