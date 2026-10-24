namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// Which review a reviewing role supplies. A proposal needs one of each before it is settled, so
/// the two are slots rather than a list: naming the kind is what lets a surface say which slot a
/// reviewer's verdict filled. It is meaningless on a role that does not terminate on a review.
/// </summary>
public enum RoleReviewKind
{
    /// <summary>This role supplies no review.</summary>
    None,

    /// <summary>The review of the change itself: what the code does and whether it holds up.</summary>
    Dev,

    /// <summary>The review of the result: whether what was built is the thing that was asked for.</summary>
    Pm
}
