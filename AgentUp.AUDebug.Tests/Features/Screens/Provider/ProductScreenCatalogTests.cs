using System.Text.Json;
using AgentUp.AUDebug.Features.Screens.Models;
using AgentUp.AUDebug.Features.Screens.Providers;
using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Screens.Provider;

/// <summary>
/// The catalog is a second statement of the design system's Assembled screens, so it is held
/// against the manifest the design-system build generates rather than trusted.
/// </summary>
[TestFixture]
public sealed class ProductScreenCatalogTests
{
    [Test]
    public void Catalog_coversEveryPageAssemblyScreenOnBothSurfaces()
    {
        var catalog = new ProductScreenCatalog();
        var documented = DocumentedScreenIds();

        var covered = catalog.Surfaces
            .SelectMany(catalog.Screens)
            .Select(screen => screen.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(covered, Is.EquivalentTo(documented),
            "au-debug screens must capture exactly the screens the design system documents under Assembled screens.");
    }

    [Test]
    public void Catalog_statesWhyEachUncapturedScreenHasNoDemoState()
    {
        var catalog = new ProductScreenCatalog();

        var unavailable = catalog.Surfaces
            .SelectMany(catalog.Screens)
            .Where(screen => !screen.Available)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(unavailable.Select(screen => screen.View),
                Is.EquivalentTo(new[] { "diagnostics", "metrics", "validation", "database" }));
            Assert.That(unavailable.All(screen => screen.UnavailableReason.Length > 0), Is.True);
            Assert.That(unavailable.All(screen => screen.Steps.Count == 0), Is.True);
            Assert.That(
                unavailable.Single(screen => screen.View == "validation").UnavailableReason,
                Does.Contain("sidebar"));
        });
    }

    [Test]
    public void Catalog_givesEveryCapturedScreenAtLeastOneInteraction()
    {
        var catalog = new ProductScreenCatalog();

        var inert = catalog.Surfaces
            .SelectMany(catalog.Screens)
            .Where(screen => screen.Available && screen.Steps.Count == 0)
            .Select(screen => screen.Id)
            .ToArray();

        Assert.That(inert, Is.Empty, "Every captured screen is captured in a used state, so every route has steps.");
    }

    [Test]
    public void Catalog_leavesInnerMobilePagesByRoutingInsteadOfTheBackControl()
    {
        var taps = new ProductScreenCatalog()
            .Screens(ProductSurface.Mobile)
            .SelectMany(screen => screen.Steps)
            .Where(step => step.Kind == ScreenStepKind.Tap)
            .Select(step => step.Target)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(taps, Does.Contain("Open Codex"));
            Assert.That(taps, Does.Not.Contain("Go back"));
            Assert.That(taps, Does.Not.Contain("Codex"));
        });
    }

    [Test]
    public void Catalog_sizesDesktopAndMobileForTheirOwnSurface()
    {
        var catalog = new ProductScreenCatalog();

        Assert.Multiple(() =>
        {
            Assert.That(catalog.Screens(ProductSurface.Desktop).Select(screen => screen.Width).Distinct(), Is.EqualTo(new[] { 1440 }));
            Assert.That(catalog.Screens(ProductSurface.Mobile).Select(screen => screen.Width).Distinct(), Is.EqualTo(new[] { 390 }));
        });
    }

    private static IReadOnlyCollection<string> DocumentedScreenIds()
    {
        var root = RepositoryRootProvider.Find(TestContext.CurrentContext.TestDirectory)
                   ?? throw new InvalidOperationException("Could not find the repository root.");
        var manifest = Path.Join(root, "AgentUp.DesignSystem", "dist", "web", "screenshots.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return document.RootElement.GetProperty("scenes")
            .EnumerateArray()
            .Select(scene => scene.GetProperty("id").GetString()!)
            .ToArray();
    }
}
