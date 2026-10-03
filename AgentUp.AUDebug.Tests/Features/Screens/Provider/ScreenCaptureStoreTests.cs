using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

[TestFixture]
public sealed class ScreenCaptureStoreTests
{
    [Test]
    public void Reset_emptiesTheSurfaceDirectorySoAStaleScreenCannotSurvive()
    {
        var paths = new FakePathValidator(NewRoot());
        var store = new ScreenCaptureStore(paths);
        var directory = store.Reset(ProductSurface.Desktop);
        File.WriteAllText(Path.Join(directory, "stale.png"), "x");

        store.Reset(ProductSurface.Desktop);

        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [Test]
    public void CapturePath_namesOnePngPerViewUnderItsSurface()
    {
        var store = new ScreenCaptureStore(new FakePathValidator(NewRoot()));

        var path = store.CapturePath(ProductSurface.Mobile, "review");

        Assert.Multiple(() =>
        {
            Assert.That(path, Does.EndWith(Path.Join("mobile", "review.png")));
            Assert.That(path, Does.Contain(Path.Join("artifacts", "product-screens")));
        });
    }

    [Test]
    public void WriteManifest_recordsEveryScreenIncludingTheSkippedOnes()
    {
        var root = NewRoot();
        var store = new ScreenCaptureStore(new FakePathValidator(root));
        var manifest = new ScreenRunManifestDto("Demo", "harbor-shop",
        [
            new ScreenCaptureDto("desktop-git", "desktop", "git", "Desktop Git changes", "artifacts/product-screens/desktop/git.png", null),
            new ScreenCaptureDto("desktop-validation", "desktop", "validation", "Desktop validation", null, "Demo hides it")
        ]);

        var written = File.ReadAllText(store.WriteManifest(manifest));

        Assert.Multiple(() =>
        {
            Assert.That(written, Does.Contain("desktop-validation"));
            Assert.That(written, Does.Contain("Demo hides it"));
            Assert.That(written, Does.Contain("harbor-shop"));
        });
    }

    [Test]
    public void Relative_reportsPathsFromTheRepositoryRoot()
    {
        var root = NewRoot();
        var store = new ScreenCaptureStore(new FakePathValidator(root));

        var relative = store.Relative(Path.Join(root, "artifacts", "product-screens", "desktop", "git.png"));

        Assert.That(relative, Is.EqualTo(Path.Join("artifacts", "product-screens", "desktop", "git.png")));
    }

    private static string NewRoot()
    {
        var root = Path.Join(Path.GetTempPath(), $"au-debug-screens-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
