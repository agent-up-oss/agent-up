namespace AgentUp.Server.Features.Agents.Models;

public sealed record AssignedIssue(
    string Id,
    int Number,
    string Title,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Url);
