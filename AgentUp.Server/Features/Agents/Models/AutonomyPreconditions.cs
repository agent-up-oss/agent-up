namespace AgentUp.Server.Features.Agents.Models;

public static class AutonomyPreconditions
{
    public const string ForgeCapabilityEnabled = "forgeCapabilityEnabled";
    public const string HostConnectionAuthorized = "hostConnectionAuthorized";
    public const string AgentAvailable = "agentAvailable";
    public const string AutonomyEnabled = "autonomyEnabled";
    public const string WorkspaceRegistered = "workspaceRegistered";
    public const string BoundAgent = "boundAgent";
    public const string PermissionRequests = "permissionRequests";
    public const string PermissionPolicy = "permissionPolicy";
    public const string GitBranch = "gitBranch";
    public const string UnpushedCommits = "unpushedCommits";
    public const string CommitQueueEdit = "commitQueueEdit";
    public const string UnassignedChanges = "unassignedChanges";

    public const string ForgeCapabilityRemedy = "Run: agent-up capabilities bootstrap";
    public const string HostConnectionRemedy = "Sign in to your repository host.";
    public const string AgentAvailableRemedy = "Enable an agent capability";
    public const string AutonomyEnabledRemedy = "Enable autonomy for this Server";
    public const string WorkspaceRegisteredRemedy = "Register the workspace before activating the loop.";
    public const string BoundAgentRemedy = "Bind an agent to the workspace.";
    public const string PermissionRequestsRemedy =
        "Bind an agent that declares support for protocol permission requests.";
    public const string PermissionPolicyRemedy = "Add a permission policy before activating the loop.";
    public const string GitBranchRemedy = "Check out a git branch before activating the loop.";
    public const string UnpushedCommitsRemedy = "Push or remove unpushed commits before activating the loop.";
    public const string CommitQueueEditRemedy = "Finish or abort the commit queue edit session.";
    public const string UnassignedChangesRemedy = "Assign working-tree changes to a queued proposal.";

    public static string Refusal(string id) => $"{id}: {Remedy(id)}";

    public static string Remedy(string id) => id switch
    {
        ForgeCapabilityEnabled => ForgeCapabilityRemedy,
        HostConnectionAuthorized => HostConnectionRemedy,
        AgentAvailable => AgentAvailableRemedy,
        AutonomyEnabled => AutonomyEnabledRemedy,
        WorkspaceRegistered => WorkspaceRegisteredRemedy,
        BoundAgent => BoundAgentRemedy,
        PermissionRequests => PermissionRequestsRemedy,
        PermissionPolicy => PermissionPolicyRemedy,
        GitBranch => GitBranchRemedy,
        UnpushedCommits => UnpushedCommitsRemedy,
        CommitQueueEdit => CommitQueueEditRemedy,
        UnassignedChanges => UnassignedChangesRemedy,
        _ => id
    };
}
