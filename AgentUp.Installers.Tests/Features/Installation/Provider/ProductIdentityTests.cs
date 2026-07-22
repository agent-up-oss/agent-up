using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.Installers.Tests.Features.Installation.Provider;

[TestFixture]
public class ProductIdentityTests
{
    private static ProductManifest AcmeStudio
        => new("Acme Studio", "acme-studio", "ACMESTUDIO");

    [Test]
    public async Task Session_forNonAgentUpManifest_containsNoAgentUpText()
    {
        var manifest = AcmeStudio;
        var session = InstallerSession.CreateDefault(
            manifest,
            new Version(1, 0, 0),
            manifest.DefaultInstallRoot(),
            PayloadSelection.Bundled(manifest.ProductName, new Version(1, 0, 0)));
        var adapter = new FakeInstallerPlatformAdapter();

        var planTitles = string.Join(" ", adapter.PlanInstall(session).Select(op => op.Title));

        var progressMessages = new List<string>();
        await foreach (var progress in adapter.ExecuteInstallAsync(session))
            progressMessages.Add(progress.Message);

        var allText = string.Join(" ", new[]
        {
            session.ProductName,
            session.Location.RootDirectory,
            planTitles,
            string.Join(" ", progressMessages)
        });

        Assert.Multiple(() =>
        {
            Assert.That(allText, Does.Not.Contain("Agent-Up"), "No 'Agent-Up' should appear in a non-Agent-Up session");
            Assert.That(allText, Does.Not.Contain("agent-up"), "No 'agent-up' should appear in a non-Agent-Up session");
            Assert.That(allText, Does.Contain("Acme Studio").Or.Contain("acme-studio"), "Session should reference the Acme Studio product");
        });
    }

    [Test]
    public void TwoManifests_haveDistinctNonOverlappingDefaultInstallRoots()
    {
        var agentUp = ProductManifest.AgentUp();
        var acmeStudio = AcmeStudio;

        var agentUpRoot = agentUp.DefaultInstallRoot();
        var acmeStudioRoot = acmeStudio.DefaultInstallRoot();

        Assert.Multiple(() =>
        {
            Assert.That(agentUpRoot, Is.Not.EqualTo(acmeStudioRoot), "Install roots must differ");
            Assert.That(agentUpRoot, Does.Not.Contain(acmeStudio.Slug), "Agent-Up root must not contain Acme Studio's slug");
            Assert.That(acmeStudioRoot, Does.Not.Contain(agentUp.Slug), "Acme Studio root must not contain Agent-Up's slug");
        });
    }

    [Test]
    public void TwoManifests_useDistinctPayloadRootVariables_allowingIndependentOverride()
    {
        var agentUp = ProductManifest.AgentUp();
        var acmeStudio = AcmeStudio;

        Assert.Multiple(() =>
        {
            Assert.That(agentUp.PayloadRootVariable, Is.Not.EqualTo(acmeStudio.PayloadRootVariable));
            Assert.That(agentUp.PayloadRootVariable, Is.EqualTo("AGENTUP_INSTALLER_PAYLOAD_ROOT"));
            Assert.That(acmeStudio.PayloadRootVariable, Is.EqualTo("ACMESTUDIO_INSTALLER_PAYLOAD_ROOT"));
        });
    }

    private static IEnumerable<TestCaseData> ManifestPairs()
    {
        yield return new TestCaseData(ProductManifest.AgentUp(), new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO"))
            .SetName("AgentUp_vs_AcmeStudio");
    }

    [TestCaseSource(nameof(ManifestPairs))]
    public void Sessions_forTwoManifests_areFullyIsolated(ProductManifest first, ProductManifest second)
    {
        var firstRoot = first.DefaultInstallRoot();
        var secondRoot = second.DefaultInstallRoot();

        Assert.Multiple(() =>
        {
            Assert.That(firstRoot, Is.Not.EqualTo(secondRoot), "Install roots must not overlap");
            Assert.That(first.ServiceName, Is.Not.EqualTo(second.ServiceName), "Service names must not overlap");
            Assert.That(first.PayloadRootVariable, Is.Not.EqualTo(second.PayloadRootVariable), "Payload root env var names must not overlap");
            Assert.That(firstRoot, Does.Not.Contain(second.Slug), $"'{first.ProductName}' root must not contain '{second.ProductName}' slug");
            Assert.That(secondRoot, Does.Not.Contain(first.Slug), $"'{second.ProductName}' root must not contain '{first.ProductName}' slug");
        });
    }
}
