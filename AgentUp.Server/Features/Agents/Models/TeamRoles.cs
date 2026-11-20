namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The role each kind of team member plays when nobody declared one: what it is woken by, what it
/// is asked to do, and what has to have happened before its task counts as done. The Server does
/// the Git and host writes around the agent, so no prompt asks an agent to commit, push or open
/// anything itself.
/// </summary>
public static class TeamRoles
{
    public const string AuthorPrompt =
        "Edit the files in this repository to make this change, and update the tests to match. " +
        "Your changes are committed and proposed for review when you stop, so do not commit them yourself. " +
        "When you are done, reply with a short summary of what you changed.";

    public const string DevReviewerPrompt =
        "Check that the change does what the issue asks and nothing else, and look for bugs. The diff above is the whole change, so you can review it directly; you cannot change files. " +
        "Reply with your review: what is good, what must change, and a last line that is exactly VERDICT: APPROVE or VERDICT: REQUEST_CHANGES.";

    public const string PmReviewerPrompt =
        "You are the product reviewer on this team. You review what a user would see, not the code. " +
        "Start the workspace and open the application in the browser, try what the issue asks for, and judge whether it does it. " +
        "Finish with your review and a last line that is exactly VERDICT: APPROVE or VERDICT: REQUEST_CHANGES.";

    /// <summary>The declared role, or the one this member kind plays by default.</summary>
    public static AgentRoleDefinition For(TeamMemberKind member, AgentRoleDefinition? declared) =>
        declared ?? member switch
        {
            TeamMemberKind.DevReviewer => new(DevReviewerPrompt, [], RoleTriggerKind.ProposalAwaitingReview, RoleTerminationKind.ReviewSubmitted),
            TeamMemberKind.PmReviewer => new(PmReviewerPrompt, [], RoleTriggerKind.ProposalAwaitingReview, RoleTerminationKind.ReviewSubmitted),
            _ => new(AuthorPrompt, [], RoleTriggerKind.IssueAssigned, RoleTerminationKind.ProposalOpened)
        };

    /// <summary>Whether this member kind reviews other members' work rather than writing its own.</summary>
    public static bool Reviews(TeamMemberKind member) => member is TeamMemberKind.DevReviewer or TeamMemberKind.PmReviewer;
}
