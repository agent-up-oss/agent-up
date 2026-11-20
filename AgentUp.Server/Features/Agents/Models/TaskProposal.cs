namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The proposal an author's work became. <paramref name="Revision"/> counts the pushes, so a review
/// is always of one particular revision and an older verdict cannot approve newer code.
/// </summary>
public sealed record TaskProposal(string Number, string Url, string Branch, string BaseBranch, int Revision);
