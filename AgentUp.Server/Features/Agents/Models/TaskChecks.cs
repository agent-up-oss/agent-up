using AgentUp.Server.Features.Forge.DTOs;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>How the host's check results read to the task flow.</summary>
public static class TaskChecks
{
    private static readonly HashSet<string> FailedConclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "failure", "error", "timed_out", "cancelled", "action_required"
    };

    /// <summary>
    /// The checks whose latest result failed. A host lists every result a check reported, newest
    /// first, so a check that failed and then passed is not failing.
    /// </summary>
    public static IEnumerable<ForgeCheckDto> Failing(IEnumerable<ForgeCheckDto> checks) =>
        checks
            .GroupBy(check => check.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .Where(check => FailedConclusions.Contains(check.Conclusion));
}
