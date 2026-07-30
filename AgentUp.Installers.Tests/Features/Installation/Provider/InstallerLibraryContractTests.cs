using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Tests.Features.Installation.Provider;

/// <summary>
/// Verifies the standalone installer library's contract:
/// (1) theory cases cover both Agent-Up manifest and SampleProduct manifest behavioral assertions,
///     maintaining parity with the full solution's installer test run; and
/// (2) adding a new product requires only providing a ProductManifest — no library code changes.
/// </summary>
[TestFixture]
public class InstallerLibraryContractTests
{
    // The canonical theory case source shared across coverage-parity assertions.
    // Must always include at least one Agent-Up manifest entry and at least one SampleProduct entry.
    private static IEnumerable<TestCaseData> StandardManifestCases()
    {
        yield return new TestCaseData(ProductManifest.AgentUp())
            .SetName("AgentUp");
        yield return new TestCaseData(
            new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
            {
                Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
            }).SetName("SampleProduct_AcmeStudio");
    }

    // === Coverage parity (step 3) ===

    [Test]
    public void StandardManifestCases_includesAgentUpManifest()
    {
        var slugs = StandardManifestCases()
            .Select(tc => ((ProductManifest)tc.Arguments[0]!).Slug)
            .ToList();

        Assert.That(slugs, Does.Contain("agent-up"),
            "StandardManifestCases must include the Agent-Up manifest so that standalone test " +
            "runs maintain the same behavioral assertions as the full solution's installer test run.");
    }

    [Test]
    public void StandardManifestCases_includesAtLeastOneSampleProductManifest()
    {
        var slugs = StandardManifestCases()
            .Select(tc => ((ProductManifest)tc.Arguments[0]!).Slug)
            .ToList();

        Assert.That(slugs.Any(s => s != "agent-up"), Is.True,
            "StandardManifestCases must include at least one non-Agent-Up (SampleProduct) manifest " +
            "so that no coverage is silently dropped by excluding Agent-Up product projects.");
    }

    [TestCaseSource(nameof(StandardManifestCases))]
    public async Task ForBothManifests_fullInstallPlanAndProgress_areConsistentAndProductSpecific(ProductManifest manifest)
    {
        var session = InstallerSession.CreateDefault(
            manifest, new Version(1, 0, 0), manifest.DefaultInstallRoot(),
            PayloadSelection.Bundled(manifest.ProductName, new Version(1, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();

        var plan = adapter.PlanInstall(session);
        var progressEvents = new List<InstallProgress>();
        await foreach (var p in adapter.ExecuteInstallAsync(session))
            progressEvents.Add(p);
        var report = await adapter.ValidateInstalledStateAsync(session);

        Assert.Multiple(() =>
        {
            Assert.That(progressEvents, Has.Count.EqualTo(plan.Count),
                $"[{manifest.ProductName}] every planned operation must produce exactly one progress event");
            Assert.That(plan.Any(op => op.Title.Contains(manifest.ServiceName, StringComparison.Ordinal)), Is.True,
                $"[{manifest.ProductName}] install plan must reference the product's own service name");
            Assert.That(report.Succeeded, Is.True,
                $"[{manifest.ProductName}] post-install validation must succeed");
        });
    }

    [TestCaseSource(nameof(StandardManifestCases))]
    public void ForBothManifests_defaultInstallRoot_containsProductSlug_andDiffersAcrossProducts(ProductManifest manifest)
    {
        var root = manifest.DefaultInstallRoot();
        var otherManifest = manifest.Slug == "agent-up"
            ? new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
            : ProductManifest.AgentUp();

        Assert.Multiple(() =>
        {
            Assert.That(root, Does.Contain(manifest.Slug).Or.Contain(manifest.ProductName),
                $"[{manifest.ProductName}] default install root must reference the product's own identifier");
            Assert.That(root, Is.Not.EqualTo(otherManifest.DefaultInstallRoot()),
                $"[{manifest.ProductName}] default install root must differ from the other manifest's root");
        });
    }

    // === Third-product extension proof (step 5) ===

    [Test]
    public async Task AddingThirdProduct_requiresOnlyManifest_noLibraryCodeChangesNeeded()
    {
        // Arrange: define a brand-new product manifest entirely inline.
        // No changes to the AgentUp.Installers library source are required.
        var galaxyCode = new ProductManifest("Galaxy Code", "galaxy-code", "GALAXYCODE")
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
        };

        var session = InstallerSession.CreateDefault(
            galaxyCode, new Version(3, 0, 0), galaxyCode.DefaultInstallRoot(),
            PayloadSelection.Bundled(galaxyCode.ProductName, new Version(3, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();

        var plan = adapter.PlanInstall(session);
        var progressEvents = new List<InstallProgress>();
        await foreach (var p in adapter.ExecuteInstallAsync(session))
            progressEvents.Add(p);
        var report = await adapter.ValidateInstalledStateAsync(session);

        Assert.Multiple(() =>
        {
            Assert.That(plan, Is.Not.Empty,
                "Galaxy Code must produce a non-empty install plan without any library changes");
            Assert.That(progressEvents, Has.Count.EqualTo(plan.Count),
                "Every planned operation must produce a progress event for the third product");
            Assert.That(report.Succeeded, Is.True,
                "Post-install validation must succeed for the third product");
            Assert.That(string.Join(" ", plan.Select(op => op.Title)),
                Does.Not.Contain("Agent-Up"),
                "No plan item for a third product may reference 'Agent-Up'");
            Assert.That(string.Join(" ", plan.Select(op => op.Title)),
                Does.Not.Contain("agent-up"),
                "No plan item for a third product may reference 'agent-up'");
            Assert.That(string.Join(" ", plan.Select(op => op.Title)),
                Does.Contain("galaxy-code"),
                "Install plan must reference the third product's own service name");
        });
    }

    [Test]
    public void AddingThirdProduct_serviceAndCliNames_deriveFromManifestSlugAlone()
    {
        var galaxyCode = new ProductManifest("Galaxy Code", "galaxy-code", "GALAXYCODE");

        Assert.Multiple(() =>
        {
            Assert.That(galaxyCode.ServiceName, Is.EqualTo("galaxy-code-server"),
                "Service name must derive from the manifest slug without library changes");
            Assert.That(galaxyCode.CliCommandName, Is.EqualTo("galaxy-code"),
                "CLI command name must derive from the manifest slug without library changes");
            Assert.That(galaxyCode.PayloadRootVariable, Is.EqualTo("GALAXYCODE_INSTALLER_PAYLOAD_ROOT"),
                "Payload root env variable must derive from the manifest environment prefix without library changes");
        });
    }
}
