namespace AgentUp.Server.Features.Agents.Models;

/// <summary>A policy resolution: the ACP option to send back, and the rule that chose it.</summary>
public sealed record PermissionDecision(string OptionId, string RuleId);
