namespace AgentUp.Architecture.Tests.Rules;

/// <summary>One 'verification.paths' entry, as read from agent-up.json.</summary>
internal sealed record VerificationRuleShape(string Match, IReadOnlyList<string> Checks);
