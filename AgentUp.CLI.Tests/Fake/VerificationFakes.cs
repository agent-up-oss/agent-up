using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class StubVerificationConfigurationLoader(VerificationConfiguration configuration)
    : IVerificationConfigurationLoader
{
    public VerificationConfiguration Load(string repositoryRoot) => configuration;
}

internal sealed class ThrowingVerificationConfigurationLoader(string message) : IVerificationConfigurationLoader
{
    public VerificationConfiguration Load(string repositoryRoot)
        => throw new VerificationConfigurationException(message);
}

internal sealed class StaticChangedContentSource(IReadOnlyDictionary<string, string> changed) : IChangedContentSource
{
    public string Name => "test";

    public Task<IReadOnlyDictionary<string, string>> GetChangedContentHashesAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
        => Task.FromResult(changed);
}

internal sealed class InMemoryReceiptLedgerStore(ReceiptLedger? initial = null) : IReceiptLedgerStore
{
    public ReceiptLedger Ledger { get; private set; } = initial ?? ReceiptLedger.Empty;

    public Task<ReceiptLedger> ReadAsync(string repositoryRoot, CancellationToken cancellationToken)
        => Task.FromResult(Ledger);

    public Task WriteAsync(string repositoryRoot, ReceiptLedger ledger, CancellationToken cancellationToken)
    {
        Ledger = ledger;
        return Task.CompletedTask;
    }
}

internal sealed class FakePlatformCapabilityProvider(string platformId = "linux") : IPlatformCapabilityProvider
{
    public string PlatformId => platformId;

    public bool IsContinuousIntegration => false;
}

internal sealed class FakeVerificationClock : IVerificationClock
{
    public DateTimeOffset UtcNow => new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
}

internal sealed class ScriptedCheckRunner(IReadOnlyDictionary<string, int>? exitCodes = null) : ICheckRunner
{
    private readonly List<string> _executed = [];

    public IReadOnlyList<string> Executed => _executed;

    public Task<CheckOutcome> RunAsync(
        string repositoryRoot,
        CheckDefinition check,
        CancellationToken cancellationToken)
    {
        _executed.Add(check.Id);
        var exitCode = exitCodes is not null && exitCodes.TryGetValue(check.Id, out var configured) ? configured : 0;
        return Task.FromResult(new CheckOutcome(check.Id, check.Command, exitCode, 5, "runner output"));
    }
}
