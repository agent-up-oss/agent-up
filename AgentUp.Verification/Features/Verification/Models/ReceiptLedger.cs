namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// All receipts recorded for one checkout. Stored outside the working tree so a receipt
/// can never travel in a commit and satisfy another machine's guard.
/// </summary>
public sealed record ReceiptLedger(IReadOnlyList<VerificationReceipt> Receipts)
{
    public static readonly ReceiptLedger Empty = new([]);

    /// <summary>
    /// Replaces any existing receipt for the same check, so the ledger holds at most one
    /// receipt per check and cannot accumulate stale successes alongside fresh failures.
    /// </summary>
    public ReceiptLedger With(VerificationReceipt receipt)
        => new([
            .. Receipts.Where(existing => !string.Equals(existing.CheckId, receipt.CheckId, StringComparison.Ordinal)),
            receipt
        ]);

    public VerificationReceipt? Find(string checkId)
        => Receipts.FirstOrDefault(receipt => string.Equals(receipt.CheckId, checkId, StringComparison.Ordinal));
}
