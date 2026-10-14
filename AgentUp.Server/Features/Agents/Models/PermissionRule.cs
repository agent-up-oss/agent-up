namespace AgentUp.Server.Features.Agents.Models;

public sealed record PermissionRule(
    string Id,
    PermissionRuleKind Kind,
    bool Allow,
    string? Scope = null);
