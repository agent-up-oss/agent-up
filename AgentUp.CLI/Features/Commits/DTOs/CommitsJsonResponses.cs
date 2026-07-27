namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitsStatusJson(
    int Count,
    IReadOnlyList<CommitsStatusEntryJson> Entries,
    IReadOnlyList<string> UnassignedFiles,
    CommitsStatusSessionJson? ActiveSession);

public sealed record CommitsStatusEntryJson(
    string Id,
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tests);

public sealed record CommitsStatusSessionJson(
    string EntryId,
    IReadOnlyList<string> Files);

public sealed record CommitsNextStagedJson(
    bool Staged,
    string? Slice,
    string? Message,
    int RemainingCount);

public sealed record CommitsNextEmptyJson(
    bool Staged,
    bool Empty,
    string? Message,
    int RemainingCount);

public sealed record CommitsErrorJson(string Error);
