using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerificationGuardServiceTests
{
    private const string Root = "/repo";

    /// <summary>
    /// Builds a guard over an explicit change set and ledger. Each test states its own
    /// world here rather than inheriting one from a shared SetUp, so a test can be read
    /// top to bottom without looking anywhere else.
    /// </summary>
    private static VerificationGuardService GuardOver(
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

        return new VerificationGuardService(plans, new InMemoryReceiptLedgerStore(ledger));
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
    public async Task GuardAsync_passesWhenNothingChanged()
    {
        var guard = GuardOver(
            VerificationDomain.Configuration().Build(),
            new ChangeSetBuilder().Build(),
            ReceiptLedger.Empty);

        var report = await guard.GuardAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(report.Satisfied, Is.True);
            Assert.That(report.Verdicts, Is.Empty);
        });
    }

    [Test]
    public async Task GuardAsync_reportsNeverRunForARequiredCheckWithNoReceipt()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var guard = GuardOver(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build(),
            ReceiptLedger.Empty);

        var report = await guard.GuardAsync(Root);

        Assert.That(
            report.Verdicts.Single(verdict => verdict.CheckId == VerificationDomain.ServerUnitCheck).Kind,
            Is.EqualTo(CheckVerdictKind.NeverRun));
    }

    [Test]
    public async Task GuardAsync_satisfiesACheckWhoseReceiptMatchesTheCurrentBytes()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var ledger = ReceiptLedger.Empty
            .With(ReceiptBuilder.Proving(PlanFor(configuration, changed, VerificationDomain.ServerUnitCheck)).Build())
            .With(ReceiptBuilder.Proving(PlanFor(configuration, changed, VerificationDomain.ArchitectureCheck)).Build());

        var report = await GuardOver(configuration, changed, ledger).GuardAsync(Root);

        Assert.That(report.Satisfied, Is.True);
    }

    [Test]
    public async Task GuardAsync_reportsStaleWhenACoveredFileChangedAfterTheCheckRan()
    {
        // The loophole this closes: run the checks, then keep editing, then stop.
        var configuration = VerificationDomain.Configuration().Build();
        var beforeEdit = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var ledger = ReceiptLedger.Empty
            .With(ReceiptBuilder.Proving(PlanFor(configuration, beforeEdit, VerificationDomain.ServerUnitCheck)).Build());

        var afterEdit = new ChangeSetBuilder().Edited(VerificationDomain.ServerSource).Build();
        var report = await GuardOver(configuration, afterEdit, ledger).GuardAsync(Root);

        var verdict = report.Verdicts.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck);
        Assert.Multiple(() =>
        {
            Assert.That(verdict.Kind, Is.EqualTo(CheckVerdictKind.Stale));
            Assert.That(verdict.Detail, Does.Contain(VerificationDomain.ServerSource));
        });
    }

    [Test]
    public async Task GuardAsync_reportsStaleWhenANewFileJoinedTheChangeSet()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var narrow = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var ledger = ReceiptLedger.Empty
            .With(ReceiptBuilder.Proving(PlanFor(configuration, narrow, VerificationDomain.ServerUnitCheck)).Build());

        var widened = ChangeSetBuilder.Changing(VerificationDomain.ServerSource, VerificationDomain.ServerTest).Build();
        var report = await GuardOver(configuration, widened, ledger).GuardAsync(Root);

        Assert.That(
            report.Verdicts.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck).Kind,
            Is.EqualTo(CheckVerdictKind.Stale));
    }

    [Test]
    public async Task GuardAsync_reportsStaleWhenTheCheckCommandChanged()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var plan = PlanFor(configuration, changed, VerificationDomain.ServerUnitCheck);
        var ledger = ReceiptLedger.Empty
            .With(ReceiptBuilder.Proving(plan).WithCommand("dotnet test --filter Nothing").Build());

        var report = await GuardOver(configuration, changed, ledger).GuardAsync(Root);

        Assert.That(
            report.Verdicts.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck).Kind,
            Is.EqualTo(CheckVerdictKind.Stale));
    }

    [Test]
    public async Task GuardAsync_reportsFailedWhenTheReceiptRecordsANonZeroExit()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build();
        var plan = PlanFor(configuration, changed, VerificationDomain.ServerUnitCheck);
        var ledger = ReceiptLedger.Empty.With(ReceiptBuilder.Proving(plan).WithExitCode(1).Build());

        var report = await GuardOver(configuration, changed, ledger).GuardAsync(Root);

        Assert.That(
            report.Verdicts.Single(item => item.CheckId == VerificationDomain.ServerUnitCheck).Kind,
            Is.EqualTo(CheckVerdictKind.Failed));
    }

    [Test]
    public async Task GuardAsync_reportsSkippedWithAReasonForAnotherPlatformsSmoke()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build();

        var report = await GuardOver(configuration, changed, ReceiptLedger.Empty, VerificationDomain.Linux, isContinuousIntegration: true)
            .GuardAsync(Root);

        var verdict = report.Verdicts.Single(item => item.CheckId == VerificationDomain.MacOsSmokeCheck);
        Assert.Multiple(() =>
        {
            Assert.That(verdict.Kind, Is.EqualTo(CheckVerdictKind.Skipped));
            Assert.That(verdict.Blocking, Is.False);
            Assert.That(verdict.Detail, Does.Contain("macos"));
        });
    }

    [Test]
    public async Task GuardAsync_blocksOnFilesThatMatchedNoRule()
    {
        var configuration = VerificationDomain.Configuration().Build();
        var changed = ChangeSetBuilder.Changing(VerificationDomain.UnmappedSource).Build();

        var report = await GuardOver(configuration, changed, ReceiptLedger.Empty).GuardAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(report.UnmatchedFiles, Is.EqualTo(new[] { VerificationDomain.UnmappedSource }));
            Assert.That(report.Satisfied, Is.False);
        });
    }

    [Test]
    public async Task GuardAsync_carriesTheConfiguredEnforcementIntoTheReport()
    {
        var configuration = VerificationDomain.Configuration()
            .WithEnforcement(VerificationEnforcement.Warn)
            .Build();

        var report = await GuardOver(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build(),
            ReceiptLedger.Empty).GuardAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(report.Satisfied, Is.False);
            Assert.That(report.ShouldBlock, Is.False, "Warn enforcement reports without failing.");
        });
    }

    [Test]
    public async Task GuardAsync_countsTheChangedFilesItConsidered()
    {
        var report = await GuardOver(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource, VerificationDomain.MobileSource).Build(),
            ReceiptLedger.Empty).GuardAsync(Root);

        Assert.That(report.ChangedFileCount, Is.EqualTo(2));
    }
}
