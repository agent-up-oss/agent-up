using LocalInstaller.App.Features.Installation.Controllers;
using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.App.Tests.Features.Installation.Controller;

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
        ProductComponent.Cli,
        ProductComponent.Tray
    ];

    private static readonly IReadOnlyList<ProductComponent> MultiCliComponents =
    [
        new("admin-cli", "Admin CLI", "Admin command surface.", InstallerComponentTarget.Cli),
        new("user-cli", "User CLI", "User command surface.", InstallerComponentTarget.Cli)
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
    public void TryComponentAction_acceptsAllAgentUpComponentIds()
    {
        foreach (var id in new[] { "desktop", "server", "cli", "tray" })
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

    [Test]
    public void TryComponentAction_acceptsExplicitId_whenMultipleOptionsShareTarget()
    {
        var found = InstallerCommandLineParser.TryComponentAction(
            ["--install-component", "admin-cli"], "--install-component", MultiCliComponents, out var component);

        Assert.That(found, Is.True);
        Assert.That(component.Id, Is.EqualTo("admin-cli"));
    }

    [Test]
    public void TryComponentAction_rejectsTargetAlias_whenMultipleOptionsShareTarget()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InstallerCommandLineParser.TryComponentAction(
                ["--install-component", "cli"], "--install-component", MultiCliComponents, out _));

        Assert.That(ex!.Message, Does.Contain("ambiguous"));
        Assert.That(ex.Message, Does.Contain("admin-cli"));
        Assert.That(ex.Message, Does.Contain("user-cli"));
    }
}
