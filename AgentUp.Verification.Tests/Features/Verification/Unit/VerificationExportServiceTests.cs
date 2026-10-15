using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerificationExportServiceTests
{
    private const string Root = "/repo";
    private const string Revision = "abc123def456";

    private static VerificationExportService ExportOver(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        ReceiptLedger ledger,
        string platformId = VerificationDomain.Linux,
        bool isContinuousIntegration = false)
    {
        var plans = new VerificationPlanService(
            new StubConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(platformId, isContinuousIntegration)),
            [new StaticChangedContentSource("test", changed)]);

        return new VerificationExportService(
            plans,
            new VerificationGuardService(plans, new InMemoryReceiptLedgerStore(ledger)),
            new InMemoryReceiptLedgerStore(ledger),
            FakeVerificationClock.AtNoon(),
            new FixedGitRevisionProvider(Revision),
            new CoveredDigestProvider());
    }

    private static PlannedCheck PlanFor(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        string checkId,
        string platformId = VerificationDomain.Linux)
        => new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(platformId))
            .CreatePlan(configuration, changed)
            .Checks
            .Single(check => check.CheckId == checkId);

    [Test]
    public async Task ExportAsync_reportsSatisfiedWhenTheReceiptMatches()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var planned = PlanFor(configuration, changed, VerificationDomain.ServerUnitCheck);
        var export = await ExportOver(configuration, changed, new ReceiptLedger([ReceiptBuilder.Proving(planned).Build()]))
            .ExportAsync(Root);

        var check = export.Checks.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck);
        Assert.Multiple(() =>
        {
            Assert.That(check.Status, Is.EqualTo("satisfied"));
            Assert.That(check.ExitCode, Is.Zero);
            Assert.That(export.Revision, Is.EqualTo(Revision));
            Assert.That(export.CoveredDigest, Does.StartWith("sha256:"));
        });
    }

    [Test]
    public async Task ExportAsync_reportsStaleWhenTheCoveredBytesDrifted()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var planned = PlanFor(configuration, changed, VerificationDomain.ServerUnitCheck);
        var stale = ReceiptBuilder.Proving(planned)
            .WithCovered(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [VerificationDomain.ServerSource] = "sha256:old"
            })
            .Build();

        var export = await ExportOver(configuration, changed, new ReceiptLedger([stale])).ExportAsync(Root);

        Assert.That(
            export.Checks.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck).Status,
            Is.EqualTo("stale"));
    }

    [Test]
    public async Task ExportAsync_reportsSkippedWithAReasonAndNeverAsProven()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build();

        var export = await ExportOver(configuration, changed, ReceiptLedger.Empty).ExportAsync(Root);

        var skipped = export.Checks.Single(item => item.CheckId == VerificationDomain.MacOsSmokeCheck);
        Assert.Multiple(() =>
        {
            Assert.That(skipped.Status, Is.EqualTo("skipped"));
            Assert.That(skipped.SkipReason, Is.EqualTo("ciOnly"));
            Assert.That(skipped.Status, Is.Not.EqualTo("satisfied"));
        });
    }

    [Test]
    public async Task ExportAsync_reportsNeverRunWhenThereIsNoReceipt()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();

        var export = await ExportOver(configuration, changed, ReceiptLedger.Empty).ExportAsync(Root);

        Assert.That(
            export.Checks.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck).Status,
            Is.EqualTo("neverRun"));
    }

    [Test]
    public void Export_hasNoCoveredFileHashMap()
    {
        Assert.That(
            typeof(VerificationExport).GetProperties().Select(property => property.Name),
            Does.Not.Contain("Covered"));
        Assert.That(
            typeof(VerificationExportCheck).GetProperties().Select(property => property.Name),
            Does.Not.Contain("Covered"));
    }
}
