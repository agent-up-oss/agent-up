using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// Builds a <see cref="VerificationConfiguration"/>, letting a test start from the shared
/// domain shape and override only what it is exercising.
/// </summary>
internal sealed class VerificationConfigurationBuilder
{
    private readonly List<CheckBuilder> _checks = [];
    private readonly List<VerificationPathRule> _paths = [];
    private readonly List<string> _always = [];
    private VerificationEnforcement _enforcement = VerificationEnforcement.Block;

    public VerificationConfigurationBuilder WithCheck(CheckBuilder check)
    {
        _checks.RemoveAll(existing => string.Equals(existing.Id, check.Id, StringComparison.Ordinal));
        _checks.Add(check);
        return this;
    }

    public VerificationConfigurationBuilder WithPathRule(string match, params string[] checks)
    {
        _paths.Add(new VerificationPathRule(match, checks));
        return this;
    }

    /// <summary>Replaces the rules for a glob already present, preserving order.</summary>
    public VerificationConfigurationBuilder ReplacingPathRule(string match, params string[] checks)
    {
        var index = _paths.FindIndex(rule => string.Equals(rule.Match, match, StringComparison.Ordinal));
        if (index >= 0)
            _paths[index] = new VerificationPathRule(match, checks);

        return this;
    }

    public VerificationConfigurationBuilder WithAlways(params string[] checks)
    {
        _always.AddRange(checks);
        return this;
    }

    public VerificationConfigurationBuilder WithoutAlways()
    {
        _always.Clear();
        return this;
    }

    public VerificationConfigurationBuilder WithEnforcement(VerificationEnforcement enforcement)
    {
        _enforcement = enforcement;
        return this;
    }

    public VerificationConfiguration Build()
        => new(
            _enforcement,
            _always,
            _checks.ToDictionary(check => check.Id, check => check.Build(), StringComparer.Ordinal),
            _paths);
}
