using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ScreenshotAppContractProviderTests
{
    [Test]
    public void Verify_acceptsCatalogComponentHtml()
    {
        var root = Repo();
        ArrangeValid(root);
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);

        Assert.DoesNotThrow(() => new ScreenshotAppContractProvider(paths, media).Verify(Scene()));
    }

    [Test]
    public void Verify_rejectsUnknownClass()
    {
        var root = Repo();
        ArrangeValid(root, html: """<div class="au-workspace">checkout-fix</div><div class="au-not-a-catalog"></div>""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);

        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("not in the design-system catalog"));
    }

    [Test]
    public void Verify_missingHtml_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        File.Delete(Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots", "desktop-workspaces.html"));
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("Screenshot HTML"));
    }

    [Test]
    public void Verify_missingComponentInHtml_throws()
    {
        var root = Repo();
        ArrangeValid(root, html: """<div class="au-workspace"></div>""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("missing catalog component"));
    }

    [Test]
    public void Verify_unknownComponentId_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { Components = ["not-a-component"] }),
            Throws.InvalidOperationException.With.Message.Contains("unknown catalog component"));
    }

    [Test]
    public void Verify_emptyComponents_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { Components = [] }),
            Throws.InvalidOperationException.With.Message.Contains("does not name catalog components"));
    }

    [Test]
    public void Verify_missingCss_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        File.Delete(Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots.css"));
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("screenshot CSS is missing"));
    }

    [Test]
    public void Verify_catalogWithoutSurfaces_throws()
    {
        var root = Repo();
        ArrangeValid(root, catalog: """{ }""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("missing surfaces"));
    }

    [Test]
    public void Verify_catalogWithNoComponents_throws()
    {
        var root = Repo();
        ArrangeValid(root, catalog: """{ "surfaces": [] }""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("has no components"));
    }

    [Test]
    public void Verify_layoutShellMissingFromHtml_throws()
    {
        var root = Repo();
        ArrangeValid(
            root,
            html: """<div class="au-workspace">checkout-fix</div>""",
            css: ".au-workspace { color: black; } .au-screen { color: black; }",
            catalog: """{ "surfaces": [ { "components": [ { "id": "screen", "html": "<div class=\"au-screen\"></div>" }, { "id": "workspace", "html": "<div class=\"au-workspace\">checkout-fix</div>" } ] } ] }""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { Components = ["screen", "workspace"] }),
            Throws.InvalidOperationException.With.Message.Contains("missing catalog component 'screen'"));
    }

    [Test]
    public void Verify_layoutShell_acceptsFilledRoot()
    {
        var root = Repo();
        ArrangeValid(
            root,
            html: """<div class="au-screen"><div class="au-workspace">checkout-fix</div></div>""",
            css: ".au-workspace { color: black; } .au-screen { color: black; }",
            catalog: """{ "surfaces": [ { "components": [ { "id": "screen", "html": "<div class=\"au-screen\"></div>" }, { "id": "workspace", "html": "<div class=\"au-workspace\">checkout-fix</div>" } ] } ] }""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);

        Assert.DoesNotThrow(() => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { Components = ["screen", "workspace"] }));
    }

    [Test]
    public void Verify_repositoryScenesComposeCatalog()
    {
        var root = RepositoryRootProvider.Find(TestContext.CurrentContext.TestDirectory)
                   ?? throw new InvalidOperationException("Could not find agent-up.sln.");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        var contract = new ScreenshotAppContractProvider(paths, media);
        var scenes = new ScreenshotManifestStore(paths).Read().Scenes;

        Assert.That(scenes, Has.Count.GreaterThanOrEqualTo(20));
        foreach (var scene in scenes)
            contract.Verify(scene);
    }

    private static AgentUp.AUDebug.Features.Screenshots.DTOs.ScreenshotSceneDto Scene()
        => new()
        {
            Id = "desktop-workspaces",
            Surface = "desktop",
            View = "workspaces",
            HtmlFile = "desktop-workspaces.html",
            MediaFile = "desktop-workspaces.png",
            Components = ["workspace"]
        };

    private static string Repo()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        return root;
    }

    private static void ArrangeValid(string root, string? html = null, string? css = null, string? catalog = null)
    {
        var dist = Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots");
        Directory.CreateDirectory(dist);
        File.WriteAllText(Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots.css"), css ?? ".au-workspace { color: black; }");
        File.WriteAllText(
            Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "catalog.json"),
            catalog ?? """{ "surfaces": [ { "components": [ { "id": "workspace", "html": "<div class=\"au-workspace\">checkout-fix</div>" } ] } ] }""");
        File.WriteAllText(
            Path.Join(dist, "desktop-workspaces.html"),
            html ?? """<div class="au-workspace">checkout-fix</div>""");
    }
}
