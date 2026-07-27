namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record EnqueueRequest(
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tests);
