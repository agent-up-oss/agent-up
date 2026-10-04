using System.ComponentModel;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Commits.Controllers;

[McpServerToolType]
public sealed class CommitQueueMcpTools(CommitQueueMcpService service)
{
    /// <remarks>
    /// Both target parameters carry the same rule, so the rule is written once. An agent
    /// reads it per parameter in the advertised schema either way.
    /// </remarks>
    private const string WorkspaceIdDescription =
        "Registered workspace id, as returned by start_workspace or list_workspaces. Pass exactly one of "
        + "workspaceId or worktreePath, and omit both when the session is bound to a workspace.";

    private const string WorktreePathDescription =
        "Absolute path to the repository worktree on the Server host. Pass exactly one of workspaceId or "
        + "worktreePath, and omit both when the session is bound to a workspace.";

    [McpServerTool(Name = "enqueue_commit", Title = "Enqueue Commit")]
    [Description("Use at the end of a task to declare a vertical-slice proposal for developer review. When commits.enabled is true, required verification runs and the proposal becomes a commit in the Server-managed dependent queue; use the structured queueWorktreePath in this tool's result for all later work. Legacy queues save and restore an independent patch. Conventional messages must be scoped to the queued slice, such as fix(Commits): validate queue metadata. Prefixes must be correct: feat is a user-facing addition, fix is a user-facing fix, test is a test-only or smoke-validation change, chore is maintenance/packaging/CI/tooling with no customer runtime effect, refactor is an internal no-behavior source change, style is CSS/HTML only, and docs is documentation only. Follow prompts.commitPolicy in agent-up.json. Do NOT call git add, git commit, or git stash directly.")]
    public Task<McpToolResult> EnqueueCommit(
        [Description("Short slice label identifying the logical unit of change, e.g. 'Commits' or 'UbuntuInstallation'.")] string slice,
        [Description("Conventional commit message scoped to the queued slice, e.g. fix(Commits): validate queue metadata. Use feat for user-facing additions, fix for user-facing fixes, test for test-only or smoke-validation changes, chore for maintenance/packaging/CI/tooling with no customer runtime effect, refactor for internal no-behavior source changes, style for CSS/HTML only, and docs for documentation only.")] string message,
        [Description("Repo-relative file paths to include in this commit entry. At least one required.")] IReadOnlyList<string> files,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.EnqueueCommit(slice, message, files, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "enqueue_review_fix_commit", Title = "Enqueue Review Fix Commit")]
    [Description("Use when fixing pull request review feedback. Enqueues exactly one review issue violation fix with a required stable reviewIssueId. Do not combine multiple review issues in one commit. Use a conventional commit message scoped to the queued slice. Use fix for user-facing fixes, test for test-only or smoke-validation changes, chore for maintenance/packaging/CI/tooling with no customer runtime effect, refactor for internal no-behavior source changes, style for CSS/HTML only, and docs for documentation only.")]
    public Task<McpToolResult> EnqueueReviewFixCommit(
        [Description("Stable review issue or review-thread id represented by this single queued commit.")] string reviewIssueId,
        [Description("Short slice label identifying the logical unit of change, e.g. 'Commits'.")] string slice,
        [Description("Conventional commit message scoped to the queued slice, e.g. fix(Commits): validate queue metadata. Use feat for user-facing additions, fix for user-facing fixes, test for test-only or smoke-validation changes, chore for maintenance/packaging/CI/tooling with no customer runtime effect, refactor for internal no-behavior source changes, style for CSS/HTML only, and docs for documentation only.")] string message,
        [Description("Repo-relative file paths to include in this review-fix commit entry. At least one required.")] IReadOnlyList<string> files,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.EnqueueReviewFixCommit(reviewIssueId, slice, message, files, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "get_commits_status", Title = "Get Commits Status")]
    [Description("Returns the current commit queue: queued entries with their files and messages, unassigned modified files, and any active edit session. Run this after enqueueing so the developer can see the queue before stopping.")]
    public Task<McpToolResult> GetCommitsStatus(
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.GetCommitsStatus(workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "guard_commits", Title = "Guard Commit Queue")]
    [Description("Use before starting a new coding task and before publishing work. A dependent proposal queue does not block later tasks: success returns continueWorktreePath and the agent must switch to that managed queue tip. Legacy queued entries, active edit sessions, staged changes, unassigned modified files, and active Git operations still block work.")]
    public Task<McpToolResult> GuardCommits(
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.GuardCommits(workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "get_commit_changes", Title = "Get Commit Changes")]
    [Description("Shows working-tree files with queue assignment information. Use instead of shelling out to git status, git ls-files, find, or similar commands for commit queue decisions.")]
    public Task<McpToolResult> GetCommitChanges(
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.GetCommitChanges(workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "inspect_commit", Title = "Inspect Commit Entry")]
    [Description("Shows one queued commit entry by index or id. Use includePatch only when the patch content is needed for review or debugging.")]
    public Task<McpToolResult> InspectCommit(
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Whether to include the saved patch text in the result.")] bool includePatch,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.InspectCommit(entryRef, includePatch, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "update_commit_message", Title = "Update Commit Message")]
    [Description("Updates the conventional commit message for a queued entry without editing the queue file directly. The message must remain scoped to the queued slice. Prefixes must follow Agent-Up rules: feat is a user-facing addition, fix is a user-facing fix, test is a test-only or smoke-validation change, chore is maintenance/packaging/CI/tooling with no customer runtime effect, refactor is an internal no-behavior source change, style is CSS/HTML only, and docs is documentation only.")]
    public Task<McpToolResult> UpdateCommitMessage(
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Replacement conventional commit message using the correct Agent-Up prefix rules.")] string message,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.UpdateCommitMessage(entryRef, message, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "add_commit_files", Title = "Add Commit Files")]
    [Description("Adds repo-relative files to a queued entry. Use when same-slice files were missed; files can only belong to one queued entry.")]
    public Task<McpToolResult> AddCommitFiles(
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Repo-relative file paths to add to the queued entry.")] IReadOnlyList<string> files,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.AddCommitFiles(entryRef, files, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "remove_commit_files", Title = "Remove Commit Files")]
    [Description("Removes repo-relative files from a queued entry without editing the queue file directly.")]
    public Task<McpToolResult> RemoveCommitFiles(
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description("Repo-relative file paths to remove from the queued entry.")] IReadOnlyList<string> files,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.RemoveCommitFiles(entryRef, files, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "remove_commit", Title = "Remove Commit Entry")]
    [Description("Archives one queued entry without staging it. Prefer this over manually editing queue files.")]
    public Task<McpToolResult> RemoveCommit(
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.RemoveCommit(entryRef, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "restore_commit", Title = "Restore Commit Entry")]
    [Description("Restores one archived queued entry by id.")]
    public Task<McpToolResult> RestoreCommit(
        [Description("Archived entry id.")] string entryId,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.RestoreCommit(entryId, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "clear_commits", Title = "Clear Commit Queue")]
    [Description("Archives all queued entries without staging anything. Use only when explicitly asked to clear or discard the queue.")]
    public Task<McpToolResult> ClearCommits(
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.ClearCommits(workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "begin_commit_edit", Title = "Begin Commit Edit")]
    [Description("Temporarily applies one queued entry back into a clean working tree so it can be changed. Use only to inspect, debug, or continue existing queued work.")]
    public Task<McpToolResult> BeginCommitEdit(
        [Description("Queued entry index, starting at 1, or queued entry id.")] string entryRef,
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.BeginCommitEdit(entryRef, workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "save_commit_edit", Title = "Save Commit Edit")]
    [Description("Saves the active commit edit session as a new queued patch and restores the tracked files. Fails if edits include files outside the active entry.")]
    public Task<McpToolResult> SaveCommitEdit(
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.SaveCommitEdit(workspaceId, worktreePath, cancellationToken);

    [McpServerTool(Name = "abort_commit_edit", Title = "Abort Commit Edit")]
    [Description("Aborts the active commit edit session, restores the working tree for that entry, and keeps the original queued patch.")]
    public Task<McpToolResult> AbortCommitEdit(
        [Description(WorkspaceIdDescription)] string? workspaceId = null,
        [Description(WorktreePathDescription)] string? worktreePath = null,
        CancellationToken cancellationToken = default)
        => service.AbortCommitEdit(workspaceId, worktreePath, cancellationToken);
}
