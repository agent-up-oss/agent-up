using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Services;

namespace AgentUp.Server.Features.Commits.Controllers;

public sealed class CommitsController(CommitsService service)
{
    public Task<CommitsEnqueueResult> EnqueueAsync(string worktreePath, EnqueueRequest request, CancellationToken cancellationToken = default)
        => service.EnqueueAsync(worktreePath, request, cancellationToken);

    public Task<CommitsStatusResult> GetStatusAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.GetStatusAsync(worktreePath, cancellationToken);

    public Task<CommitChangesResult> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.GetChangesAsync(worktreePath, cancellationToken);

    public Task<CommitInspectResult> InspectAsync(string worktreePath, string entryRef, bool includePatch, CancellationToken cancellationToken = default)
        => service.InspectAsync(worktreePath, entryRef, includePatch, cancellationToken);

    public Task<CommitEditResult> UpdateMessageAsync(string worktreePath, string entryRef, string message, CancellationToken cancellationToken = default)
        => service.UpdateMessageAsync(worktreePath, entryRef, message, cancellationToken);

    public Task<CommitEditResult> SetTestsAsync(string worktreePath, string entryRef, IReadOnlyList<string> tests, CancellationToken cancellationToken = default)
        => service.SetTestsAsync(worktreePath, entryRef, tests, cancellationToken);

    public Task<CommitEditResult> AddFilesAsync(string worktreePath, string entryRef, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        => service.AddFilesAsync(worktreePath, entryRef, files, cancellationToken);

    public Task<CommitEditResult> RemoveFilesAsync(string worktreePath, string entryRef, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        => service.RemoveFilesAsync(worktreePath, entryRef, files, cancellationToken);

    public Task<CommitEditResult> RemoveAsync(string worktreePath, string entryRef, CancellationToken cancellationToken = default)
        => service.RemoveAsync(worktreePath, entryRef, cancellationToken);

    public Task<CommitEditResult> RestoreArchivedAsync(string worktreePath, string entryId, CancellationToken cancellationToken = default)
        => service.RestoreArchivedAsync(worktreePath, entryId, cancellationToken);

    public Task<CommitEditResult> ClearAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.ClearAsync(worktreePath, cancellationToken);

    public Task<CommitEditResult> BeginEditAsync(string worktreePath, string entryRef, CancellationToken cancellationToken = default)
        => service.BeginEditAsync(worktreePath, entryRef, cancellationToken);

    public Task<CommitEditResult> SaveEditAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.SaveEditAsync(worktreePath, cancellationToken);

    public Task<CommitEditResult> AbortEditAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.AbortEditAsync(worktreePath, cancellationToken);

    public Task<CommitGuardResult> GuardAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.GuardAsync(worktreePath, cancellationToken);
}
