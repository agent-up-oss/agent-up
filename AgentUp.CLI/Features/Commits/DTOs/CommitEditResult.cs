using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitEditResult(
    bool Success,
    string Message,
    CommitEntry? Entry = null,
    CommitEditSession? Session = null)
{
    public static CommitEditResult Completed(string message, CommitEntry? entry = null, CommitEditSession? session = null)
        => new(true, message, entry, session);

    public static CommitEditResult Blocked(string message)
        => new(false, message);
}
