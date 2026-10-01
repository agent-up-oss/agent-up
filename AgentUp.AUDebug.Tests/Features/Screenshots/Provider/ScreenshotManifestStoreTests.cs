using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ScreenshotManifestStoreTests
{
    [Test]
    public void Read_parsesScenes()
    {
        var root = Repo();
        WriteManifest(root, """{ "scenes": [ { "id": "desktop-git", "surface": "desktop", "view": "git", "title": "Git", "mediaFile": "desktop-git.png", "htmlFile": "desktop-git.html", "width": 1440, "height": 900, "hero": false, "livePath": "", "appSources": ["a.axaml"], "requiredClasses": ["au-git-row"], "requiredDesktopClasses": ["gitNodeRow"], "requiredMobileComponents": [], "copy": ["Harbor Shop"] } ] }""");

        var manifest = new ScreenshotManifestStore(new DebugPathValidator(root)).Read();

        Assert.That(manifest.Scenes, Has.Count.EqualTo(1));
        Assert.That(manifest.Scenes[0].Id, Is.EqualTo("desktop-git"));
        Assert.That(manifest.Scenes[0].RequiredClasses[0], Is.EqualTo("au-git-row"));
        Assert.That(manifest.Scenes[0].Copy[0], Is.EqualTo("Harbor Shop"));
    }

    [Test]
    public void Read_missingManifest_throws()
    {
        var root = Repo();
        Assert.That(
            () => new ScreenshotManifestStore(new DebugPathValidator(root)).Read(),
            Throws.InvalidOperationException.With.Message.Contains("build design-system"));
    }

    [Test]
    public void Read_emptyScenes_throws()
    {
        var root = Repo();
        WriteManifest(root, """{ "scenes": [] }""");
        Assert.That(
            () => new ScreenshotManifestStore(new DebugPathValidator(root)).Read(),
            Throws.InvalidOperationException.With.Message.Contains("has no scenes"));
    }

    private static string Repo()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-manifest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        return root;
    }

    private static void WriteManifest(string root, string json)
    {
        var path = Path.Join(root, "AgentUp.DesignSystem", "dist", "web");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Join(path, "screenshots.json"), json);
    }
}
