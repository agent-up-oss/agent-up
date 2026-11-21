namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The proposal an author's work became. <paramref name="Revision"/> counts the pushes, so a review
/// is always of one particular revision and an older verdict cannot approve newer code.
/// <paramref name="Head"/> is the commit that revision's branch pointed at on the host; a push
/// that moves it, by the Server or by anyone else, is the next revision.
/// </summary>
public sealed record TaskProposal(string Number, string Url, string Branch, string BaseBranch, int Revision, string? Head = null);
