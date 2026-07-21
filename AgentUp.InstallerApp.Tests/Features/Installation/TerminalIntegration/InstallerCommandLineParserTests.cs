using AgentUp.InstallerApp.Features.Installation.Controllers;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Tests.Features.Installation.TerminalIntegration;

[TestFixture]
public class InstallerCommandLineParserTests
{
    private static readonly ProductComponent Editor = new("editor", "Editor", "Visual editing surface.");
    private static readonly ProductComponent Renderer = new("renderer", "Renderer", "Output renderer.");
    private static readonly IReadOnlyList<ProductComponent> TwoComponents = [Editor, Renderer];

    private static readonly IReadOnlyList<ProductComponent> AgentUpComponents =
    [
        ProductComponent.Desktop,
        ProductComponent.Server,
        ProductComponent.Cli
    ];

    [Test]
    public void TryComponentAction_returnsFalse_whenArgumentAbsent()
    {
        var found = InstallerCommandLineParser.TryComponentAction(
            ["--validate-installed"], "--install-component", TwoComponents, out var component);

        Assert.That(found, Is.False);
        Assert.That(component, Is.Null);
    }

    [Test]
    public void TryComponentAction_returnsComponent_whenIdMatchesDeclaredComponent()
    {
        var found = InstallerCommandLineParser.TryComponentAction(
            ["--install-component", "editor"], "--install-component", TwoComponents, out var component);

        Assert.That(found, Is.True);
        Assert.That(component.Id, Is.EqualTo("editor"));
        Assert.That(component.DisplayName, Is.EqualTo("Editor"));
    }

    [Test]
    public void TryComponentAction_matchesIdCaseInsensitively()
    {
        var found = InstallerCommandLineParser.TryComponentAction(
            ["--install-component", "RENDERER"], "--install-component", TwoComponents, out var component);

        Assert.That(found, Is.True);
        Assert.That(component.Id, Is.EqualTo("renderer"));
    }

    [Test]
    public void TryComponentAction_throws_whenIdIsNotDeclaredByProduct()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InstallerCommandLineParser.TryComponentAction(
                ["--install-component", "desktop"], "--install-component", TwoComponents, out _));

        Assert.That(ex!.Message, Does.Contain("desktop"));
        Assert.That(ex.Message, Does.Contain("editor"));
        Assert.That(ex.Message, Does.Contain("renderer"));
    }

    [Test]
    public void TryComponentAction_throws_whenValueIsMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InstallerCommandLineParser.TryComponentAction(
                ["--install-component"], "--install-component", TwoComponents, out _));

        Assert.That(ex!.Message, Does.Contain("--install-component"));
    }

    [Test]
    public void TryComponentAction_acceptsAllThreeAgentUpComponentIds()
    {
        foreach (var id in new[] { "desktop", "server", "cli" })
        {
            var found = InstallerCommandLineParser.TryComponentAction(
                ["--install-component", id], "--install-component", AgentUpComponents, out var component);

            Assert.That(found, Is.True, $"Expected '{id}' to be accepted");
            Assert.That(component.Id, Is.EqualTo(id));
        }
    }

    [Test]
    public void TryComponentAction_rejectsAgentUpIds_whenRunningForDifferentProduct()
    {
        foreach (var id in new[] { "desktop", "server", "cli" })
        {
            Assert.Throws<InvalidOperationException>(() =>
                InstallerCommandLineParser.TryComponentAction(
                    ["--install-component", id], "--install-component", TwoComponents, out _),
                $"Expected '{id}' to be rejected for editor/renderer product");
        }
    }
}
