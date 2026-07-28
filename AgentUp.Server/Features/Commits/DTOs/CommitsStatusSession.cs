namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitsStatusSession(
    string EntryId,
    IReadOnlyList<string> Files);
