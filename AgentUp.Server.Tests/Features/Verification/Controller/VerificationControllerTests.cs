using AgentUp.Server.Features.Verification.Controllers;
using AgentUp.Server.Features.Verification.Services;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Verification.Controller;

[TestFixture]
public sealed class VerificationControllerTests
{
    [Test]
    public async Task RunAndGuardAsync_forwardsToTheQueueGate()
    {
        var definition = new CheckDefinition("unit", "test", null, CheckTier.Fast, [], false, 0, []);
        var configuration = new VerificationConfiguration(
            VerificationEnforcement.Block,
            [],
            new Dictionary<string, CheckDefinition> { ["unit"] = definition },
            [new VerificationPathRule("**/*.cs", ["unit"])]);
        var plans = new VerificationPlanService(
            new ConfigurationLoader(configuration),
            new CheckPlanProvider(new PathGlobProvider(), new Platform()),
            [new ChangedSource(new Dictionary<string, string> { ["src/a.cs"] = "sha256:value" })]);
        var ledger = new MemoryLedger();
        var controller = new VerificationController(new VerificationQueueGateService(
            plans,
            new VerificationRunService(plans, ledger, new PassingRunner(), new Clock()),
            new VerificationGuardService(plans, ledger)));

        var result = await controller.RunAndGuardAsync("/repo");

        Assert.That(result.Succeeded, Is.True);
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
