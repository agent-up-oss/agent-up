using AgentUp.InstallerApp.Features.Capabilities.Factories;
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
            CapabilityDashboardServiceFactory.CreateFake());

        Assert.That(model.ComponentCards.Select(c => c.Target.Id), Is.EqualTo(new[] { "editor", "renderer" }));
        Assert.That(model.ComponentCards.Select(c => c.Title), Is.EqualTo(new[] { "Editor", "Renderer" }));
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
            CapabilityDashboardServiceFactory.CreateFake());

        Assert.That(model.ComponentCards, Has.Count.EqualTo(3));
        Assert.That(model.ComponentCards.Select(c => c.Target.Id), Is.EqualTo(new[] { "desktop", "server", "cli" }));
    }
}
