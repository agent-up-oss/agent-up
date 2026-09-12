using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>
/// Durable storage for verification receipts, scoped to one checkout.
/// </summary>
public interface IReceiptLedgerStore
{
    Task<ReceiptLedger> ReadAsync(string repositoryRoot, CancellationToken cancellationToken);

    Task WriteAsync(string repositoryRoot, ReceiptLedger ledger, CancellationToken cancellationToken);
}
