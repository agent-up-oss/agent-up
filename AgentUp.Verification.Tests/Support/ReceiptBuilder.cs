using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// Builds a <see cref="VerificationReceipt"/>. Defaults to a successful receipt that
/// covers exactly what it is given, which is the case most tests vary from.
/// </summary>
internal sealed class ReceiptBuilder(string checkId)
{
    private string _command = "true";
    private int _exitCode;
    private string _ranAtUtc = "2026-09-11T18:00:00.0000000+00:00";
    private long _durationMs = 1200;
    private IReadOnlyDictionary<string, string> _covered = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Records this receipt as proving exactly the given plan's covered map.</summary>
    public static ReceiptBuilder Proving(PlannedCheck check)
        => new ReceiptBuilder(check.CheckId)
            .WithCommand(check.Definition.Command)
            .WithCovered(check.Covered);

    public ReceiptBuilder WithCommand(string command)
    {
        _command = command;
        return this;
    }

    public ReceiptBuilder WithExitCode(int exitCode)
    {
        _exitCode = exitCode;
        return this;
    }

    public ReceiptBuilder WithRanAtUtc(string ranAtUtc)
    {
        _ranAtUtc = ranAtUtc;
        return this;
    }

    public ReceiptBuilder WithDurationMs(long durationMs)
    {
        _durationMs = durationMs;
        return this;
    }

    public ReceiptBuilder WithCovered(IReadOnlyDictionary<string, string> covered)
    {
        _covered = covered;
        return this;
    }

    public VerificationReceipt Build()
        => new(checkId, _command, _exitCode, _ranAtUtc, _durationMs, _covered);
}
