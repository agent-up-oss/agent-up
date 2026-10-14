namespace AgentUp.Server.Features.Agents.Models;

public sealed record AgentRoleDefinition(
    string Prompt,
    IReadOnlyList<string> Tools,
    RoleTriggerKind Trigger,
    RoleTerminationKind Termination);
