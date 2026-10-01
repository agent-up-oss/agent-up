using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ScreenshotAppContractProviderTests
{
    [Test]
    public void Verify_acceptsCatalogClassAndFakeServerCopy()
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
        ArrangeValid(root, html: """<div class="au-not-a-catalog"></div>""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);

        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { RequiredClasses = [] }),
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
    public void Verify_missingRequiredClass_throws()
    {
        var root = Repo();
        ArrangeValid(root, html: """<div class="au-workspace">Harbor Shop</div>""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { RequiredClasses = ["au-git-row"] }),
            Throws.InvalidOperationException.With.Message.Contains("missing required catalog class"));
    }

    [Test]
    public void Verify_missingCopyInHtml_throws()
    {
        var root = Repo();
        ArrangeValid(root, html: """<div class="au-workspace"></div>""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("missing Demo copy"));
    }

    [Test]
    public void Verify_missingDesktopClass_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        File.WriteAllText(Path.Join(root, "AgentUp.Desktop", "MainWindow.axaml"), "<Border />");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("Desktop class"));
    }

    [Test]
    public void Verify_missingMobileComponent_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene() with { RequiredMobileComponents = ["mobileTabBar"] }),
            Throws.InvalidOperationException.With.Message.Contains("auBox('mobileTabBar')"));
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
    public void Verify_missingFakeServer_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        File.Delete(Path.Join(root, "AgentUp.FakeServer", "definition.json"));
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("definition.json is missing"));
    }

    [Test]
    public void Verify_missingAppSource_throws()
    {
        var root = Repo();
        ArrangeValid(root);
        File.Delete(Path.Join(root, "AgentUp.Desktop", "MainWindow.axaml"));
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("App source"));
    }

    [Test]
    public void Verify_acceptsQuotedAuBox()
    {
        var root = Repo();
        ArrangeValid(root);
        var mobile = Path.Join(root, "AgentUp.Mobile");
        Directory.CreateDirectory(mobile);
        File.WriteAllText(Path.Join(mobile, "Tabs.tsx"), """auBox("mobileTabBar")""");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);
        Assert.DoesNotThrow(() => new ScreenshotAppContractProvider(paths, media).Verify(
            Scene() with
            {
                AppSources = ["AgentUp.Mobile/Tabs.tsx"],
                RequiredDesktopClasses = [],
                RequiredMobileComponents = ["mobileTabBar"]
            }));
    }

    [Test]
    public void Verify_rejectsCopyMissingFromFakeServer()
    {
        var root = Repo();
        ArrangeValid(root, definition: "{ \"displayName\": \"Other Shop\" }");
        var paths = new DebugPathValidator(root);
        var media = new ScreenshotMediaStore(paths);

        Assert.That(
            () => new ScreenshotAppContractProvider(paths, media).Verify(Scene()),
            Throws.InvalidOperationException.With.Message.Contains("FakeServer"));
    }

    [Test]
    public void Verify_repositoryScenesMatchApps()
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
            AppSources = ["AgentUp.Desktop/MainWindow.axaml"],
            RequiredClasses = ["au-workspace"],
            RequiredDesktopClasses = ["wsEntry"],
            RequiredMobileComponents = [],
            Copy = ["Harbor Shop"]
        };

    private static string Repo()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        return root;
    }

    private static void ArrangeValid(string root, string? html = null, string? definition = null)
    {
        var dist = Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots");
        Directory.CreateDirectory(dist);
        File.WriteAllText(Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots.css"), ".au-workspace { color: black; }");
        File.WriteAllText(
            Path.Join(dist, "desktop-workspaces.html"),
            html ?? """<div class="au-workspace">Harbor Shop</div>""");
        var desktop = Path.Join(root, "AgentUp.Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Join(desktop, "MainWindow.axaml"), """<Border Classes="wsEntry" />""");
        var fake = Path.Join(root, "AgentUp.FakeServer");
        Directory.CreateDirectory(fake);
        File.WriteAllText(Path.Join(fake, "definition.json"), definition ?? """{ "displayName": "Harbor Shop" }""");
    }
}
