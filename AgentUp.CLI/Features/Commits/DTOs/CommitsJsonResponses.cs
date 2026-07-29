namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitsStatusJson(
    int Count,
    IReadOnlyList<CommitsStatusEntryJson> Entries,
    IReadOnlyList<string> UnassignedFiles,
    CommitsStatusSessionJson? ActiveSession,
    GitOperationStateJson? OperationState);

public sealed record CommitsStatusEntryJson(
    string Id,
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tests,
    string? ReviewIssueId);

public sealed record CommitsStatusSessionJson(
    string EntryId,
    IReadOnlyList<string> Files);

public sealed record GitOperationStateJson(string Kind, bool Blocking);

public sealed record CommitsNextStagedJson(
    bool Staged,
    string? Slice,
    string? Message,
    int RemainingCount);

public sealed record CommitsNextBlockedJson(
    bool Staged,
    bool Blocked,
    string Message,
    int RemainingCount);

public sealed record CommitsNextEmptyJson(
    bool Staged,
    bool Empty,
    string? Message,
    int RemainingCount);

public sealed record CommitsErrorJson(string Error);
