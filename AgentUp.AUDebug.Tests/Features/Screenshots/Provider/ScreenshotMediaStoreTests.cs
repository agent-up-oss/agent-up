using AgentUp.AUDebug.Features.Screenshots.DTOs;
using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

[TestFixture]
public sealed class ScreenshotMediaStoreTests
{
    [Test]
    public void Paths_stayUnderMediaAndDist()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-media", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        var store = new ScreenshotMediaStore(new DebugPathValidator(root));
        var scene = new ScreenshotSceneDto { HtmlFile = "desktop-git.html", MediaFile = "desktop-git.png" };

        Assert.That(store.HtmlPath(scene), Does.EndWith(Path.Join("screenshots", "desktop-git.html")));
        Assert.That(store.MediaPath(scene.MediaFile), Does.EndWith(Path.Join("media", "desktop-git.png")));
        Assert.That(store.HeroPath(), Does.EndWith(Path.Join("media", "screenshot.png")));
        Assert.That(store.FileUrl(store.HeroPath()), Does.StartWith("file://"));
    }

    [Test]
    public void Copy_writesDestination()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-media", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, "media"));
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        var store = new ScreenshotMediaStore(new DebugPathValidator(root));
        var source = Path.Join(root, "media", "desktop-applications.png");
        File.WriteAllBytes(source, [1, 2, 3]);
        store.Copy(source, store.HeroPath());

        Assert.That(File.ReadAllBytes(store.HeroPath()), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }
}
