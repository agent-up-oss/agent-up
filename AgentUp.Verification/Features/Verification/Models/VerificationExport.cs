namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// A report of what was proven about the current working tree. Evidence to read, never an
/// input to <see cref="VerificationGuardService"/>.
/// </summary>
/// <remarks>
/// Deliberately carries a digest over the covered file hashes rather than the map itself, so
/// the document cannot be fed back into the local ledger. The two types stay distinct so that
/// mistake is not expressible.
/// </remarks>
public sealed record VerificationExport(
    string Revision,
    string CoveredDigest,
    string ExportedAtUtc,
    int ChangedFileCount,
    IReadOnlyList<string> UnmatchedFiles,
    IReadOnlyList<VerificationExportCheck> Checks);
