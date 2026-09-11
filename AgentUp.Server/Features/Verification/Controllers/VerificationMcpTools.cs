using System.ComponentModel;
using AgentUp.Server.Features.Verification.Services;
using AgentUp.Server.Shared.Interfaces;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Verification.Controllers;

[McpServerToolType]
public sealed class VerificationMcpTools(VerificationMcpService service)
{
    [McpServerTool(Name = "plan_verification", Title = "Plan Verification")]
    [Description("Shows which checks the current changes require, and why each was selected. Selection comes from the static path rules in agent-up.json, not from you: you cannot narrow the set, only see it. Use this to know what will have to pass before you finish.")]
    public Task<McpToolResult> PlanVerification(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.PlanVerification(worktreePath, cancellationToken);

    [McpServerTool(Name = "run_verification", Title = "Run Verification")]
    [Description("Runs every check the current changes require and records a receipt for each. Call this at the end of a task, before enqueueing commits, so the receipts cover the code while it is still in the working tree. Stops at the first failing check. Checks that cannot run on this platform are skipped and stay required in CI.")]
    public Task<McpToolResult> RunVerification(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.RunVerification(worktreePath, cancellationToken);

    [McpServerTool(Name = "run_verification_check", Title = "Run One Verification Check")]
    [Description("Re-runs a single required check by id after a targeted fix, instead of repeating the whole set. The check must already be required by the current changes.")]
    public Task<McpToolResult> RunVerificationCheck(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        [Description("Check id as reported by plan_verification, for example 'server' or 'mobile'.")] string checkId,
        CancellationToken cancellationToken)
        => service.RunVerificationCheck(worktreePath, checkId, cancellationToken);

    [McpServerTool(Name = "guard_verification", Title = "Guard Verification")]
    [Description("Checks whether every check the current changes require has a passing receipt that matches the current file contents. Fails when a check has never run, failed, or ran against different bytes than are there now. This runs nothing itself, so it is cheap; when it reports unproven checks, call run_verification.")]
    public Task<McpToolResult> GuardVerification(
        [Description("Absolute path to the repository worktree.")] string worktreePath,
        CancellationToken cancellationToken)
        => service.GuardVerification(worktreePath, cancellationToken);
}
