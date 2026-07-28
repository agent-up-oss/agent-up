namespace AgentUp.Server.Features.Commits.Models;

public sealed record ArchivedCommitEntry(
    CommitEntry Entry,
    string ArchivedAt);
