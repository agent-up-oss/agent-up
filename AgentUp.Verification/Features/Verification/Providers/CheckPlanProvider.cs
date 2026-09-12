using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Turns a changed-file set into the checks it requires, using only the static rules.
/// </summary>
/// <remarks>
/// This type takes no caller-supplied check list on purpose. Callers may ask what is
/// required; they cannot propose a smaller answer, which is what keeps test selection out
/// of an agent's hands.
/// </remarks>
public sealed class CheckPlanProvider(PathGlobProvider globs, IPlatformCapabilityProvider platform)
{
    public VerificationPlan CreatePlan(
        VerificationConfiguration configuration,
        IReadOnlyDictionary<string, string> changedFiles)
    {
        if (changedFiles.Count == 0 || !configuration.IsConfigured)
            return VerificationPlan.Nothing;

        var normalized = changedFiles.ToDictionary(
            entry => PathGlobProvider.Normalize(entry.Key),
            entry => entry.Value,
            StringComparer.Ordinal);

        var matches = normalized.Keys
            .Select(path => (Path: path, Rules: MatchingRules(configuration, path)))
            .ToArray();

        var unmatched = matches
            .Where(match => match.Rules.Count == 0)
            .Select(match => match.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var selections = matches
            .SelectMany(match => match.Rules.SelectMany(rule =>
                rule.Checks.Select(checkId => (CheckId: checkId, match.Path, rule.Match))))
            .ToArray();

        // "always" checks lead, in the order the repository declared them, because they are
        // the foundational ones: a full-solution build must precede the suites that assume
        // it compiles. Everything else follows in id order so plans stay comparable.
        var selectedIds = selections
            .Select(selection => selection.CheckId)
            .Distinct(StringComparer.Ordinal)
            .Except(configuration.Always, StringComparer.Ordinal)
            .OrderBy(OrderOf(configuration))
            .ThenBy(checkId => checkId, StringComparer.Ordinal);

        var requiredIds = configuration.Always
            .Distinct(StringComparer.Ordinal)
            .Concat(selectedIds)
            .ToArray();

        var checks = requiredIds
            .Where(configuration.Checks.ContainsKey)
            .Select(checkId => CreatePlannedCheck(
                configuration,
                checkId,
                normalized,
                selections
                    .Where(selection => string.Equals(selection.CheckId, checkId, StringComparison.Ordinal))
                    .ToArray()))
            .ToArray();

        return new VerificationPlan(checks, normalized, unmatched);
    }

    /// <summary>
    /// Declared order for a check, so one that consumes another's output can be made to
    /// follow it. Unknown ids sort first and are filtered out later anyway.
    /// </summary>
    private static Func<string, int> OrderOf(VerificationConfiguration configuration)
        => checkId => configuration.Checks.TryGetValue(checkId, out var check) ? check.Order : 0;

    private IReadOnlyList<VerificationPathRule> MatchingRules(
        VerificationConfiguration configuration,
        string path)
        => [.. configuration.Paths.Where(rule => globs.Matches(rule.Match, path))];

    private PlannedCheck CreatePlannedCheck(
        VerificationConfiguration configuration,
        string checkId,
        IReadOnlyDictionary<string, string> changedFiles,
        IReadOnlyList<(string CheckId, string Path, string Match)> selections)
    {
        var definition = configuration.Checks[checkId];
        var isAlways = configuration.Always.Contains(checkId, StringComparer.Ordinal);

        var selectedPaths = selections.Select(selection => selection.Path);
        var inputPaths = changedFiles.Keys.Where(path => IsUnderAnyInput(definition.Inputs, path));
        var alwaysPaths = isAlways ? changedFiles.Keys : [];

        var coveredPaths = selectedPaths
            .Concat(inputPaths)
            .Concat(alwaysPaths)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        var covered = coveredPaths.ToDictionary(
            path => path,
            path => changedFiles[path],
            StringComparer.Ordinal);

        var selectedBy = selections
            .Select(selection => selection.Match)
            .Concat(isAlways ? ["always"] : [])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new PlannedCheck(definition, covered, ResolveSkipReason(definition), selectedBy);
    }

    private CheckSkipReason ResolveSkipReason(CheckDefinition definition)
    {
        if (definition.CiOnly && !platform.IsContinuousIntegration)
            return CheckSkipReason.CiOnly;

        if (definition.Platforms.Count > 0
            && !definition.Platforms.Contains(platform.PlatformId, StringComparer.Ordinal))
        {
            return CheckSkipReason.PlatformMismatch;
        }

        return CheckSkipReason.None;
    }

    private static bool IsUnderAnyInput(IReadOnlyList<string> inputs, string path)
        => inputs.Any(input =>
            string.Equals(path, input, StringComparison.Ordinal)
            || path.StartsWith(input + "/", StringComparison.Ordinal));
}
