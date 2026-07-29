using System.ComponentModel;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Commits.Controllers;

[McpServerToolType]
public sealed class CommitQueueMcpTools(CommitQueueMcpService service)
{
    [McpServerTool(Name = "enqueue_commit", Title = "Enqueue Commit")]
    [Description("Use at the end of a task to declare a vertical-slice commit for developer review. Enqueues files and a commit message into the commit queue; the files are saved as a patch and restored to their pre-change state. Conventional commit prefixes must be correct: feat is a user-facing addition, fix is a user-facing fix, chore is an internal non-runtime change, refactor is an internal no-behavior change, style is CSS/HTML only, and docs is documentation only. The developer then runs 'agentup commits next' to stage and commit each entry. Do NOT call git add, git commit, or git stash directly.")]
    public Task<McpToolResult> EnqueueCommit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Short slice label identifying the logical unit of change, e.g. 'feat/auth-middleware'.")] string slice,
        [Description("Conventional commit message using the correct prefix: feat for user-facing additions, fix for user-facing fixes, chore for internal non-runtime changes, refactor for internal no-behavior changes, style for CSS/HTML only, docs for documentation only.")] string message,
        [Description("Repo-relative file paths to include in this commit entry. At least one required.")] IReadOnlyList<string> files,
        [Description("Optional test commands to attach to this entry, e.g. 'dotnet test'. The developer sees these as a checklist before committing.")] IReadOnlyList<string>? tests,
        CancellationToken cancellationToken)
        => service.EnqueueCommit(worktreePath, slice, message, files, tests, cancellationToken);

    [McpServerTool(Name = "enqueue_review_fix_commit", Title = "Enqueue Review Fix Commit")]
    [Description("Use when fixing pull request review feedback. Enqueues exactly one review issue violation fix with a required stable reviewIssueId. Do not combine multiple review issues in one commit. Use the correct conventional commit prefix: fix for user-facing fixes, chore/refactor for internal no-behavior changes, style for CSS/HTML only, and docs for documentation only.")]
    public Task<McpToolResult> EnqueueReviewFixCommit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Stable review issue or review-thread id represented by this single queued commit.")] string reviewIssueId,
        [Description("Short slice label identifying the logical unit of change, e.g. 'Commits'.")] string slice,
        [Description("Conventional commit message using the correct prefix: feat for user-facing additions, fix for user-facing fixes, chore for internal non-runtime changes, refactor for internal no-behavior changes, style for CSS/HTML only, docs for documentation only.")] string message,
        [Description("Repo-relative file paths to include in this review-fix commit entry. At least one required.")] IReadOnlyList<string> files,
        [Description("Optional test commands to attach to this entry, e.g. 'dotnet test'.")] IReadOnlyList<string>? tests,
        CancellationToken cancellationToken)
        => service.EnqueueReviewFixCommit(worktreePath, reviewIssueId, slice, message, files, tests, cancellationToken);

    [McpServerTool(Name = "get_commits_status", Title = "Get Commits Status")]
    [Description("Returns the current commit queue: queued entries with their files and messages, unassigned modified files, and any active edit session. Run this after enqueueing so the developer can see the queue before stopping.")]
    public Task<McpToolResult> GetCommitsStatus(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.GetCommitsStatus(worktreePath, cancellationToken);

    [McpServerTool(Name = "guard_commits", Title = "Guard Commit Queue")]
    [Description("Use before starting a new coding task and before publishing work. Fails when queued entries, active edit sessions, staged changes, or unassigned modified files exist. If it fails at task start, stop unless the user asked to inspect, debug, or continue existing queued or working-tree changes.")]
    public Task<McpToolResult> GuardCommits(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.GuardCommits(worktreePath, cancellationToken);

    [McpServerTool(Name = "get_commit_changes", Title = "Get Commit Changes")]
    [Description("Shows working-tree files with queue assignment information. Use instead of shelling out to git status, git ls-files, find, or similar commands for commit queue decisions.")]
    public Task<McpToolResult> GetCommitChanges(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.GetCommitChanges(worktreePath, cancellationToken);

    [McpServerTool(Name = "inspect_commit", Title = "Inspect Commit Entry")]
    [Description("Shows one queued commit entry by index or id. Use includePatch only when the patch content is needed for review or debugging.")]
    public Task<McpToolResult> InspectCommit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Whether to include the saved patch text in the result.")] bool includePatch,
        CancellationToken cancellationToken)
        => service.InspectCommit(worktreePath, entryRef, includePatch, cancellationToken);

    [McpServerTool(Name = "update_commit_message", Title = "Update Commit Message")]
    [Description("Updates the conventional commit message for a queued entry without editing the queue file directly. The prefix must follow Agent-Up rules: feat is a user-facing addition, fix is a user-facing fix, chore is an internal non-runtime change, refactor is an internal no-behavior change, style is CSS/HTML only, and docs is documentation only.")]
    public Task<McpToolResult> UpdateCommitMessage(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Replacement conventional commit message using the correct Agent-Up prefix rules.")] string message,
        CancellationToken cancellationToken)
        => service.UpdateCommitMessage(worktreePath, entryRef, message, cancellationToken);

    [McpServerTool(Name = "update_commit_tests", Title = "Update Commit Tests")]
    [Description("Replaces the test commands attached to a queued entry without editing the queue file directly.")]
    public Task<McpToolResult> UpdateCommitTests(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Replacement test commands for this queued entry.")] IReadOnlyList<string> tests,
        CancellationToken cancellationToken)
        => service.UpdateCommitTests(worktreePath, entryRef, tests, cancellationToken);

    [McpServerTool(Name = "add_commit_files", Title = "Add Commit Files")]
    [Description("Adds repo-relative files to a queued entry. Use when same-slice files were missed; files can only belong to one queued entry.")]
    public Task<McpToolResult> AddCommitFiles(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Repo-relative file paths to add to the queued entry.")] IReadOnlyList<string> files,
        CancellationToken cancellationToken)
        => service.AddCommitFiles(worktreePath, entryRef, files, cancellationToken);

    [McpServerTool(Name = "remove_commit_files", Title = "Remove Commit Files")]
    [Description("Removes repo-relative files from a queued entry without editing the queue file directly.")]
    public Task<McpToolResult> RemoveCommitFiles(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Repo-relative file paths to remove from the queued entry.")] IReadOnlyList<string> files,
        CancellationToken cancellationToken)
        => service.RemoveCommitFiles(worktreePath, entryRef, files, cancellationToken);

    [McpServerTool(Name = "remove_commit", Title = "Remove Commit Entry")]
    [Description("Archives one queued entry without staging it. Prefer this over manually editing queue files.")]
    public Task<McpToolResult> RemoveCommit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        CancellationToken cancellationToken)
        => service.RemoveCommit(worktreePath, entryRef, cancellationToken);

    [McpServerTool(Name = "restore_commit", Title = "Restore Commit Entry")]
    [Description("Restores one archived queued entry by id.")]
    public Task<McpToolResult> RestoreCommit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Archived entry id.")] string entryId,
        CancellationToken cancellationToken)
        => service.RestoreCommit(worktreePath, entryId, cancellationToken);

    [McpServerTool(Name = "clear_commits", Title = "Clear Commit Queue")]
    [Description("Archives all queued entries without staging anything. Use only when explicitly asked to clear or discard the queue.")]
    public Task<McpToolResult> ClearCommits(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.ClearCommits(worktreePath, cancellationToken);

    [McpServerTool(Name = "begin_commit_edit", Title = "Begin Commit Edit")]
    [Description("Temporarily applies one queued entry back into a clean working tree so it can be changed. Use only to inspect, debug, or continue existing queued work.")]
    public Task<McpToolResult> BeginCommitEdit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        CancellationToken cancellationToken)
        => service.BeginCommitEdit(worktreePath, entryRef, cancellationToken);

    [McpServerTool(Name = "save_commit_edit", Title = "Save Commit Edit")]
    [Description("Saves the active commit edit session as a new queued patch and restores the tracked files. Fails if edits include files outside the active entry.")]
    public Task<McpToolResult> SaveCommitEdit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.SaveCommitEdit(worktreePath, cancellationToken);

    [McpServerTool(Name = "abort_commit_edit", Title = "Abort Commit Edit")]
    [Description("Aborts the active commit edit session, restores the working tree for that entry, and keeps the original queued patch.")]
    public Task<McpToolResult> AbortCommitEdit(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.AbortCommitEdit(worktreePath, cancellationToken);
}
