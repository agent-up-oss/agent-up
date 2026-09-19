using AgentUp.Server.Features.Verification.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerificationQueueGateServiceTests
{
    [Test]
    public async Task RunAndGuardAsync_runsSelectedCheckAndAcceptsMatchingReceipt()
    {
        var definition = new CheckDefinition("unit", "test", null, CheckTier.Fast, [], false, 0, []);
        var configuration = new VerificationConfiguration(
            VerificationEnforcement.Block,
            [],
            new Dictionary<string, CheckDefinition> { ["unit"] = definition },
            [new VerificationPathRule("**/*.cs", ["unit"])]);
        var source = new ChangedSource(new Dictionary<string, string> { ["src/a.cs"] = "sha256:value" });
        var ledger = new MemoryLedger();
        var plans = new VerificationPlanService(
            new ConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new Platform()),
            [source]);
        var runs = new VerificationRunService(plans, ledger, new PassingRunner(), new Clock());
        var service = new VerificationQueueGateService(plans, runs, new VerificationGuardService(plans, ledger));

        var result = await service.RunAndGuardAsync("/repo");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(ledger.Value.Receipts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RunAndGuardAsync_rejectsEnabledQueueWithoutVerificationRules()
    {
        var plans = new VerificationPlanService(
            new ConfigurationLoader(VerificationConfiguration.Empty),
            new CheckPlanProvider(new PathGlobProvider(), new Platform()),
            [new ChangedSource(new Dictionary<string, string> { ["src/a.cs"] = "sha256:value" })]);
        var ledger = new MemoryLedger();
        var service = new VerificationQueueGateService(
            plans,
            new VerificationRunService(plans, ledger, new PassingRunner(), new Clock()),
            new VerificationGuardService(plans, ledger));

        var result = await service.RunAndGuardAsync("/repo");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("requires a configured verification section"));
    }

    [Test]
    public async Task RunAndGuardAsync_reportsAFailedCheck()
    {
        var service = CreateService(
            Configured(),
            new Dictionary<string, string> { ["src/a.cs"] = "sha256:value" },
            new ScriptedCheckRunner(new Dictionary<string, int> { ["unit"] = 2 }));

        var result = await service.RunAndGuardAsync("/repo");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("'unit' failed with exit code 2"));
    }

    [Test]
    public async Task RunAndGuardAsync_mapsConfigurationErrors()
    {
        var plans = new VerificationPlanService(
            new ThrowingVerificationConfigurationLoader("verification section is invalid"),
            new CheckPlanProvider(new PathGlobProvider(), new Platform()),
            [new ChangedSource(new Dictionary<string, string> { ["src/a.cs"] = "sha256:value" })]);
        var ledger = new MemoryLedger();
        var service = new VerificationQueueGateService(
            plans,
            new VerificationRunService(plans, ledger, new PassingRunner(), new Clock()),
            new VerificationGuardService(plans, ledger));

        var result = await service.RunAndGuardAsync("/repo");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Is.EqualTo("verification section is invalid"));
    }

    [Test]
    public async Task RunAndGuardAsync_mapsReceiptWriteFailures()
    {
        var io = await RunWithLedger(new ThrowingLedger(new IOException("disk")));
        var access = await RunWithLedger(new ThrowingLedger(new UnauthorizedAccessException("denied")));

        Assert.That(io.Message, Is.EqualTo("Verification receipts could not be recorded."));
        Assert.That(access.Message, Is.EqualTo("Verification receipts could not be recorded."));
    }

    private static VerificationQueueGateService CreateService(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changed,
        ICheckRunner runner,
        IReceiptLedgerStore? ledger = null)
    {
        var store = ledger ?? new MemoryLedger();
        var plans = new VerificationPlanService(
            new ConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new Platform()),
            [new ChangedSource(changed)]);
        return new VerificationQueueGateService(
            plans,
            new VerificationRunService(plans, store, runner, new Clock()),
            new VerificationGuardService(plans, store));
    }

    private static async Task<AgentUp.Server.Features.Verification.DTOs.VerificationGateResult> RunWithLedger(IReceiptLedgerStore ledger)
    {
        var service = CreateService(
            Configured(),
            new Dictionary<string, string> { ["src/a.cs"] = "sha256:value" },
            new PassingRunner(),
            ledger);
        var result = await service.RunAndGuardAsync("/repo");
        Assert.That(result.Succeeded, Is.False);
        return result;
    }

    private static VerificationConfiguration Configured()
    {
        var definition = new CheckDefinition("unit", "test", null, CheckTier.Fast, [], false, 0, []);
        return new VerificationConfiguration(
            VerificationEnforcement.Block,
            [],
            new Dictionary<string, CheckDefinition> { ["unit"] = definition },
            [new VerificationPathRule("**/*.cs", ["unit"])]);
    }

    private sealed class ConfigurationLoader(VerificationConfiguration value) : IVerificationConfigurationLoader
    {
        public VerificationConfiguration Load(string repositoryRoot) => value;
    }

    private sealed class ChangedSource(IReadOnlyDictionary<string, string> value) : IChangedContentSource
    {
        public string Name => "mock";
        public Task<IReadOnlyDictionary<string, string>> GetChangedContentHashesAsync(string repositoryRoot, CancellationToken cancellationToken)
            => Task.FromResult(value);
    }

    private sealed class MemoryLedger : IReceiptLedgerStore
    {
        public ReceiptLedger Value { get; private set; } = ReceiptLedger.Empty;
        public Task<ReceiptLedger> ReadAsync(string repositoryRoot, CancellationToken cancellationToken) => Task.FromResult(Value);
        public Task WriteAsync(string repositoryRoot, ReceiptLedger ledger, CancellationToken cancellationToken)
        {
            Value = ledger;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingLedger(Exception error) : IReceiptLedgerStore
    {
        public Task<ReceiptLedger> ReadAsync(string repositoryRoot, CancellationToken cancellationToken)
            => Task.FromResult(ReceiptLedger.Empty);

        public Task WriteAsync(string repositoryRoot, ReceiptLedger ledger, CancellationToken cancellationToken)
            => throw error;
    }

    private sealed class PassingRunner : ICheckRunner
    {
        public Task<CheckOutcome> RunAsync(string repositoryRoot, CheckDefinition check, CancellationToken cancellationToken)
            => Task.FromResult(new CheckOutcome(check.Id, check.Command, 0, 1, "passed"));
    }

    private sealed class Platform : IPlatformCapabilityProvider
    {
        public string PlatformId => "linux";
        public bool IsContinuousIntegration => false;
    }

    private sealed class Clock : IVerificationClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-09-13T00:00:00Z");
    }
}
