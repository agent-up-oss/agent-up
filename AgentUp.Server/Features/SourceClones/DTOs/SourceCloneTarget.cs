namespace AgentUp.Server.Features.SourceClones.DTOs;

public sealed record SourceCloneTarget(
    string Repository,
    string Branch,
    string DirectoryName,
    string DestinationPath);
