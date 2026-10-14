namespace AgentUp.Server.Features.Agents.Models;

public sealed record AssignedProposal(
    string Id,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    string Url);
