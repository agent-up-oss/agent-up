namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitChangeEntry(string Path, GitChangeStatus Status);
