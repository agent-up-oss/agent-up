namespace AgentUp.Server.Features.Agents.Models;

public sealed record AgentWorkspaceSettings(
    string WorkspaceId,
    IReadOnlyList<PermissionRule> PolicyRules,
    AgentRoleDefinition? Role,
    bool AutonomyActive,
    bool HostConnectionAuthorized,
    AssignedIssueOrder QueueOrder,
    LoopStatusSnapshot? Loop,
    int StandingPolicyReplies = 2);
