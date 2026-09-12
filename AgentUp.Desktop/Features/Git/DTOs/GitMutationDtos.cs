namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record GitMutationResultDto(bool Found, bool Succeeded, string? Error);

public sealed record GitFilesRequestDto(IReadOnlyList<string> Files);

public sealed record GitBranchRequestDto(string Name, bool Create);
