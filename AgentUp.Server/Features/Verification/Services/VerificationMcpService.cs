using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Shared.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Services;

namespace AgentUp.Server.Features.Verification.Services;

/// <summary>
/// The MCP-facing behaviour of the verification module: plan, run, guard.
/// </summary>
/// <remarks>
/// None of these tools accept a check list. Callers say when to act; the static rules say
/// what that means. A configuration error surfaces as a failed tool result rather than a
/// silently empty plan.
/// <para>
/// Each tool names its target by registered workspace id or by absolute worktree path, so
/// a caller that does not share the Server's filesystem can still plan and run checks.
/// </para>
/// </remarks>
public sealed class VerificationMcpService(
    VerificationPlanService plans,
    VerificationRunService runs,
    VerificationGuardService guards,
    VerificationReportService reports,
    WorkspaceTargetController workspaces)
{
    public async Task<McpToolResult> PlanVerification(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            var plan = await plans.CreatePlanAsync(target.WorktreePath!, cancellationToken);
            return new McpToolResult(
                true,
                $"{plan.Checks.Count} check(s) required by {plan.ChangedFiles.Count} changed file(s).",
                reports.ToPlanDto(plan));
        }
        catch (VerificationConfigurationException exception)
        {
            return new McpToolResult(false, exception.Message);
        }
    }

    public async Task<McpToolResult> RunVerification(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            var outcomes = await runs.RunAsync(target.WorktreePath!, cancellationToken);
            var dto = reports.ToRunDto(outcomes);
            var message = outcomes.Count == 0
                ? "Nothing to run: no check is required by the current changes."
                : dto.Succeeded
                    ? $"{outcomes.Count} check(s) passed and were recorded."
                    : $"Check '{outcomes[^1].CheckId}' failed with exit code {outcomes[^1].ExitCode}.";

            return new McpToolResult(dto.Succeeded, message, dto);
        }
        catch (VerificationConfigurationException exception)
        {
            return new McpToolResult(false, exception.Message);
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Could not record verification receipts.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Could not record verification receipts.");
        }
    }

    public async Task<McpToolResult> RunVerificationCheck(
        string checkId,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);
        if (string.IsNullOrWhiteSpace(checkId))
            return new McpToolResult(false, "checkId is required.");

        try
        {
            var outcome = await runs.RunSingleAsync(target.WorktreePath!, checkId, cancellationToken);
            if (outcome is null)
            {
                return new McpToolResult(
                    false,
                    $"Check '{checkId}' is not required by the current changes, or cannot run on this platform.");
            }

            return new McpToolResult(
                outcome.Succeeded,
                outcome.Succeeded
                    ? $"Check '{checkId}' passed and was recorded."
                    : $"Check '{checkId}' failed with exit code {outcome.ExitCode}.",
                reports.ToRunDto([outcome]));
        }
        catch (VerificationConfigurationException exception)
        {
            return new McpToolResult(false, exception.Message);
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Could not record verification receipts.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Could not record verification receipts.");
        }
    }

    public async Task<McpToolResult> GuardVerification(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            var report = await guards.GuardAsync(target.WorktreePath!, cancellationToken);
            var plan = await plans.CreatePlanAsync(target.WorktreePath!, cancellationToken);

            // A warn-enforcement repository still reports its verdicts; only the
            // success flag softens, so a guard failure is always visible.
            return new McpToolResult(
                !report.ShouldBlock,
                reports.Summarize(report),
                reports.ToGuardDto(report, plan));
        }
        catch (VerificationConfigurationException exception)
        {
            return new McpToolResult(false, exception.Message);
        }
    }

    private static McpToolResult TargetFailure(WorkspaceTargetResolution target)
        => new(false, target.Error!);
}
