using System.Text.Json;
using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

/// <summary>
/// The two manifests <c>screens compare</c> joins, read off disk.
/// </summary>
[TestFixture]
public sealed class ScreenManifestReaderTests
{
    [Test]
    public void DocumentedScreens_readsTheIdSurfaceAndCopyOfEveryScene()
    {
        var root = Repo();
        WriteDesignSystem(root, """
            { "scenes": [
              { "id": "mobile-git", "surface": "mobile", "copy": ["Git", "Commit message"] },
              { "id": "desktop-git", "surface": "desktop", "copy": ["Git changes"] }
            ] }
            """);

        var documented = new DocumentedScreensProvider(new DebugPathValidator(root)).Read();

        Assert.Multiple(() =>
        {
            Assert.That(documented.Select(scene => scene.Id), Is.EqualTo(new[] { "mobile-git", "desktop-git" }));
            Assert.That(documented[0].Surface, Is.EqualTo("mobile"));
            Assert.That(documented[0].Copy, Is.EqualTo(new[] { "Git", "Commit message" }));
        });
    }

    [Test]
    public void DocumentedScreens_sceneWithoutCopy_readsAsEmptyRatherThanThrowing()
    {
        var root = Repo();
        WriteDesignSystem(root, """{ "scenes": [ { "id": "mobile-git", "surface": "mobile" } ] }""");

        var documented = new DocumentedScreensProvider(new DebugPathValidator(root)).Read();

        Assert.That(documented[0].Copy, Is.Empty);
    }

    [Test]
    public void DocumentedScreens_missingManifest_saysToBuildTheDesignSystem()
    {
        var root = Repo();

        Assert.That(
            () => new DocumentedScreensProvider(new DebugPathValidator(root)).Read(),
            Throws.InvalidOperationException.With.Message.Contains("au-debug build design-system"));
    }

    [Test]
    public void DocumentedScreens_manifestWithoutScenes_throws()
    {
        var root = Repo();
        WriteDesignSystem(root, """{ }""");

        Assert.That(
            () => new DocumentedScreensProvider(new DebugPathValidator(root)).Read(),
            Throws.InvalidOperationException.With.Message.Contains("no scenes"));
    }

    [Test]
    public void DocumentedScreens_manifestWithAnEmptySceneList_throws()
    {
        var root = Repo();
        WriteDesignSystem(root, """{ "scenes": [] }""");

        Assert.That(
            () => new DocumentedScreensProvider(new DebugPathValidator(root)).Read(),
            Throws.InvalidOperationException.With.Message.Contains("no scenes"));
    }

    [Test]
    public void ScreenRun_readsBackTheManifestARunWrote()
    {
        var root = Repo();
        var paths = new DebugPathValidator(root);
        new ScreenCaptureStore(paths).WriteManifest(new ScreenRunManifestDto(
            "Demo",
            "harbor-shop",
            [new ScreenCaptureDto("mobile-git", "mobile", "git", "Mobile Git", "git.png", null, "Changes")]));

        var run = new ScreenRunStore(paths).Read();

        Assert.Multiple(() =>
        {
            Assert.That(run!.Server, Is.EqualTo("Demo"));
            Assert.That(run.Screens[0].Id, Is.EqualTo("mobile-git"));
            Assert.That(run.Screens[0].Text, Is.EqualTo("Changes"),
                "The text the real screen showed is what the comparison holds the documented copy against.");
        });
    }

    [Test]
    public void ScreenRun_withoutARun_readsAsNothingRatherThanThrowing()
    {
        Assert.That(new ScreenRunStore(new DebugPathValidator(Repo())).Read(), Is.Null);
    }

    [Test]
    public void ScreenRun_malformedManifest_throwsRatherThanComparingAgainstNothing()
    {
        var root = Repo();
        var path = Path.Join(root, DebugLayout.ProductScreensDirectory, DebugLayout.ProductScreensManifestFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        Assert.That(() => new ScreenRunStore(new DebugPathValidator(root)).Read(), Throws.InstanceOf<JsonException>());
    }

    private static void WriteDesignSystem(string root, string manifest)
    {
        var directory = Path.Join(root, "AgentUp.DesignSystem", "dist", "web");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, "screenshots.json"), manifest);
    }

    private static string Repo()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-screen-manifests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        return root;
    }
}
