using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeGitWorkingTreeProvider : IGitWorkingTreeProvider
{
    public List<GitChangeEntry> Changes { get; } = [];

    public GitFileDiff? FileDiff { get; set; }

    public string Commit { get; set; } = "0123456789abcdef";

    public string? Failure { get; set; }

    public string? CommittedMessage { get; private set; }

    public IReadOnlyList<string> CommittedFiles { get; private set; } = [];

    public string? LastWorktreePath { get; private set; }

    public Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        return Failure is null
            ? Task.FromResult<IReadOnlyList<GitChangeEntry>>(Changes)
            : Task.FromException<IReadOnlyList<GitChangeEntry>>(new InvalidOperationException(Failure));
    }

    public Task<GitFileDiff?> GetFileDiffAsync(string worktreePath, string filePath, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        return Failure is null
            ? Task.FromResult(FileDiff)
            : Task.FromException<GitFileDiff?>(new InvalidOperationException(Failure));
    }

    public Task<string> CommitAsync(string worktreePath, IReadOnlyList<string> files, string message, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        CommittedFiles = files;
        CommittedMessage = message;
        return Failure is null
            ? Task.FromResult(Commit)
            : Task.FromException<string>(new InvalidOperationException(Failure));
    }
}
