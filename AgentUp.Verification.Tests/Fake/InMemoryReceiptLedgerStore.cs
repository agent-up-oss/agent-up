using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Fake;

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
