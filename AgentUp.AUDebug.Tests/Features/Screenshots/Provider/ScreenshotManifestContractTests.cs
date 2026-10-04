using AgentUp.AUDebug.Features.Screenshots.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screenshots.Provider;

/// <summary>
/// Invariants of the generated manifest that the checks reading it depend on.
/// </summary>
/// <remarks>
/// <c>ScreenshotAppContractProviderTests.Verify_repositoryScenesComposeCatalog</c> already holds
/// the real scenes against the catalog. These are the two fields a check silently passes without
/// instead of failing: <c>Copy</c>, which <c>--live</c> compares against the hosted page, and
/// <c>StateModifiers</c>, which tells the contract which classes a scene was allowed to move.
/// </remarks>
[TestFixture]
public sealed class ScreenshotManifestContractTests
{
    [Test]
    public void Read_everySceneCarriesCopyForTheLiveCheck()
    {
        var scenes = Manifest().Read().Scenes;

        var withoutCopy = scenes.Where(scene => scene.Copy.Count == 0).Select(scene => scene.Id).ToArray();

        Assert.That(withoutCopy, Is.Empty,
            "--live looks for this copy on the hosted page, so a scene without it passes that check having compared nothing.");
    }

    [Test]
    public void Read_everySceneRenderingASelectedStateDeclaresTheModifier()
    {
        var scenes = Manifest().Read().Scenes;

        var undeclared = scenes
            .Where(scene => scene.StateModifiers.Count == 0)
            .Where(scene => SceneHtml(scene.HtmlFile).Contains("--selected", StringComparison.Ordinal))
            .Select(scene => scene.Id)
            .ToArray();

        Assert.That(undeclared, Is.Empty,
            "A scene that renders a selected state must declare the modifier, or the contract compares it against the wrong catalog markup.");
    }

    private static ScreenshotManifestStore Manifest()
        => new(new DebugPathValidator(RepositoryRoot()));

    private static string SceneHtml(string htmlFile)
        => File.ReadAllText(Path.Join(RepositoryRoot(), "AgentUp.DesignSystem", "dist", "web", "screenshots", htmlFile));

    private static string RepositoryRoot()
        => RepositoryRootProvider.Find(TestContext.CurrentContext.TestDirectory)
           ?? throw new InvalidOperationException("Could not find the repository root.");
}
