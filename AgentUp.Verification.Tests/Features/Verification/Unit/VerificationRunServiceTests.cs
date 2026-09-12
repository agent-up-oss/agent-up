using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerificationRunServiceTests
{
    private const string Root = "/repo";

    private static VerificationPlanService PlansOver(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        string platformId = VerificationDomain.Linux)
        => new(
            new StubConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(platformId)),
            [new StaticChangedContentSource("test", changed)]);

    [Test]
    public async Task RunAsync_executesEveryRequiredCheck()
    {
        var runner = new RecordingCheckRunner();
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
            new InMemoryReceiptLedgerStore(),
            runner,
            FakeVerificationClock.AtNoon());

        await service.RunAsync(Root);

        Assert.That(runner.Executed,
            Is.EquivalentTo(new[] { VerificationDomain.ArchitectureCheck, VerificationDomain.ServerUnitCheck }));
    }

    [Test]
    public async Task RunAsync_recordsAReceiptCoveringWhatThePlanRequired()
    {
        var ledger = new InMemoryReceiptLedgerStore();
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
            ledger,
            new RecordingCheckRunner(),
            FakeVerificationClock.AtNoon());

        await service.RunAsync(Root);

        var receipt = ledger.Ledger.Find(VerificationDomain.ServerUnitCheck);
        Assert.Multiple(() =>
        {
            Assert.That(receipt, Is.Not.Null);
            Assert.That(receipt!.Covered.Keys, Is.EqualTo(new[] { VerificationDomain.ServerSource }));
            Assert.That(receipt.RanAtUtc, Does.StartWith("2026-09-11T12:00:00"));
        });
    }

    [Test]
    public async Task RunAsync_recordsAReceiptForAFailingCheckSoItReportsFailedNotNeverRun()
    {
        var ledger = new InMemoryReceiptLedgerStore();
        var exitCodes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [VerificationDomain.ArchitectureCheck] = 1
        };
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
            ledger,
            new RecordingCheckRunner(exitCodes),
            FakeVerificationClock.AtNoon());

        await service.RunAsync(Root);

        Assert.That(ledger.Ledger.Find(VerificationDomain.ArchitectureCheck)!.Succeeded, Is.False);
    }

    [Test]
    public async Task RunAsync_stopsAtTheFirstFailureInsteadOfBurningTheWholeSuite()
    {
        var exitCodes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [VerificationDomain.ArchitectureCheck] = 1
        };
        var runner = new RecordingCheckRunner(exitCodes);
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
            new InMemoryReceiptLedgerStore(),
            runner,
            FakeVerificationClock.AtNoon());

        var outcomes = await service.RunAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(runner.Executed, Is.EqualTo(new[] { VerificationDomain.ArchitectureCheck }));
            Assert.That(outcomes, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RunAsync_doesNotExecuteChecksThatCannotRunHere()
    {
        var runner = new RecordingCheckRunner();
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build()),
            new InMemoryReceiptLedgerStore(),
            runner,
            FakeVerificationClock.AtNoon());

        await service.RunAsync(Root);

        Assert.That(runner.Executed, Does.Not.Contain(VerificationDomain.MacOsSmokeCheck));
    }

    [Test]
    public async Task RunSingleAsync_runsOnlyTheNamedCheck()
    {
        var runner = new RecordingCheckRunner();
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
            new InMemoryReceiptLedgerStore(),
            runner,
            FakeVerificationClock.AtNoon());

        var outcome = await service.RunSingleAsync(Root, VerificationDomain.ServerUnitCheck);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.Not.Null);
            Assert.That(runner.Executed, Is.EqualTo(new[] { VerificationDomain.ServerUnitCheck }));
        });
    }

    [Test]
    public async Task RunSingleAsync_returnsNullForACheckTheChangesDoNotRequire()
    {
        var service = new VerificationRunService(
            PlansOver(VerificationDomain.Configuration().Build(), ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
            new InMemoryReceiptLedgerStore(),
            new RecordingCheckRunner(),
            FakeVerificationClock.AtNoon());

        Assert.That(await service.RunSingleAsync(Root, VerificationDomain.MobileCheck), Is.Null);
    }
}
