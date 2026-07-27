namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitsStatusJson(
    int Count,
    IReadOnlyList<CommitsStatusEntryJson> Entries);

public sealed record CommitsStatusEntryJson(
    string Slice,
    string Message);

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
