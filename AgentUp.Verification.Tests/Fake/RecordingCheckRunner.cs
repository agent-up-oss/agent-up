using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Fake;

/// <summary>
/// Records which checks were asked to run, and returns the exit code the test chose for
/// each, so ordering and short-circuit behaviour are observable without real processes.
/// </summary>
internal sealed class RecordingCheckRunner(IReadOnlyDictionary<string, int>? exitCodes = null) : ICheckRunner
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
        return Task.FromResult(new CheckOutcome(check.Id, check.Command, exitCode, 42, "output"));
    }
}
