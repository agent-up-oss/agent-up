using System.Text.Json;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Stores receipts under the checkout's Git directory.
/// </summary>
/// <remarks>
/// Deliberately not under .agent-up/, which this repository commits: a committed receipt
/// would travel in a pull request and satisfy a different machine's guard against bytes
/// it never tested. The Git directory is untracked by construction and per-worktree.
/// </remarks>
public sealed class FileReceiptLedgerStore(GitDirectoryProvider gitDirectories) : IReceiptLedgerStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<ReceiptLedger> ReadAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        var path = ResolvePath(repositoryRoot);
        if (path is null || !File.Exists(path))
            return ReceiptLedger.Empty;

        var text = await File.ReadAllTextAsync(path, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<ReceiptLedger>(text, SerializerOptions) ?? ReceiptLedger.Empty;
        }
        catch (JsonException)
        {
            // A corrupted ledger means "nothing is proven", which fails safe: the guard
            // asks for the checks to run again rather than trusting unreadable receipts.
            return ReceiptLedger.Empty;
        }
    }

    public async Task WriteAsync(string repositoryRoot, ReceiptLedger ledger, CancellationToken cancellationToken)
    {
        var path = ResolvePath(repositoryRoot)
            ?? throw new InvalidOperationException(
                $"'{repositoryRoot}' is not a Git checkout, so verification receipts cannot be stored.");

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Could not resolve a receipt directory for '{repositoryRoot}'.");

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(ledger, SerializerOptions), cancellationToken);
    }

    private string? ResolvePath(string repositoryRoot)
    {
        var gitDirectory = gitDirectories.Resolve(repositoryRoot);
        return gitDirectory is null
            ? null
            : Path.Join(gitDirectory, "agent-up", "verification", "receipts.json");
    }
}
