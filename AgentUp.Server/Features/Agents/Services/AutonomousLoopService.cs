using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Thin Server loop kernel: iterate prompts against the scheduled ACP session, resolve every
/// permission through an attached policy, and stop in one typed terminal. Product HTTP and
/// forge/role surfaces stay on later issues; this is the subject the CPU-inference gate drives.
/// </summary>
public sealed class AutonomousLoopService(AgentSchedulingService scheduling, LoopInvariantGuard invariants)
{
    public async Task<LoopRunResult> RunAsync(LoopRunRequest request, CancellationToken cancellationToken)
    {
        if (!scheduling.AttachPolicy(request.WorkspaceId, request.Policy))
            return Result(request, 0, LoopTerminal.Failed, "not-found", EmptyInspection(), "workspace agent is not scheduled", 0);

        var context = request.Task.Length;
        var noProgress = 0;
        try
        {
            for (var iteration = 1; iteration <= request.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var message = iteration == 1
                    ? request.Task
                    : noProgress > 0
                        ? "Continue under the standing permission policy. Do not wait for confirmation. Call complete_task when the task is done."
                        : "Continue the task. Call complete_task when it is done.";
                context += message.Length;
                AgentActionResult action;
                using (var promptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    promptTimeout.CancelAfter(Math.Max(1, request.PromptTimeoutMs));
                    try
                    {
                        action = await scheduling.PromptWaitAsync(request.WorkspaceId, message, promptTimeout.Token);
                    }
                    catch (OperationCanceledException) when (promptTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        action = AgentActionResult.Failed("The agent stopped writing but held the session open.");
                    }

                    if (promptTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        await scheduling.CancelAsync(request.WorkspaceId, CancellationToken.None);
                        var hung = scheduling.InspectLoop(request.WorkspaceId);
                        return Result(request, iteration, LoopTerminal.Failed, "unresponsive", hung, "The agent stopped writing but held the session open.", context);
                    }
                }
                var snapshot = scheduling.InspectLoop(request.WorkspaceId);
                if (context > request.ContextCharLimit)
                    return Result(request, iteration, LoopTerminal.Failed, "context_limit", snapshot, "context truncated", context);
                if (!string.IsNullOrWhiteSpace(snapshot.BlockReason))
                    return Result(request, iteration, LoopTerminal.BlockedPolicy, snapshot.StopReason ?? "end_turn", snapshot, snapshot.BlockReason, context);
                if (action.Error is not null)
                    return Result(request, iteration, LoopTerminal.Failed, snapshot.StopReason ?? "error", snapshot, action.Error, context);
                if (snapshot.CompletedTask)
                    return Result(request, iteration, LoopTerminal.Completed, snapshot.StopReason ?? "end_turn", snapshot, null, context);
                if (string.Equals(snapshot.StopReason, "cancelled", StringComparison.Ordinal))
                    return Result(request, iteration, LoopTerminal.Cancelled, "cancelled", snapshot, null, context);
                if (string.Equals(snapshot.StopReason, "rate_limited", StringComparison.OrdinalIgnoreCase))
                    return Result(request, iteration, LoopTerminal.BlockedRateLimited, "rate_limited", snapshot, "blocked:rate-limited", context);
                if (!snapshot.SawToolCall && !snapshot.CompletedTask)
                {
                    noProgress++;
                    if (noProgress > request.StandingPolicyReplies)
                        return Result(request, iteration, LoopTerminal.BlockedAwaitingInput, "awaiting_input", snapshot, "blocked:awaiting-input", context);
                }
                else
                    noProgress = 0;
                if (iteration == request.MaxIterations)
                    return Result(
                        request,
                        iteration,
                        string.Equals(snapshot.StopReason, "end_turn", StringComparison.Ordinal) ? LoopTerminal.EndTurn : LoopTerminal.MaxIterations,
                        snapshot.StopReason ?? "max_turn_requests",
                        snapshot,
                        null,
                        context);
            }

            var last = scheduling.InspectLoop(request.WorkspaceId);
            return Result(request, request.MaxIterations, LoopTerminal.MaxIterations, last.StopReason ?? "max_turn_requests", last, null, context);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await scheduling.CancelAsync(request.WorkspaceId, CancellationToken.None);
            var cancelled = scheduling.InspectLoop(request.WorkspaceId);
            return Result(request, 0, LoopTerminal.Cancelled, "cancelled", cancelled, null, context);
        }
        finally
        {
            scheduling.AttachPolicy(request.WorkspaceId, null);
        }
    }

    public IReadOnlyList<string> Invariants(LoopRunResult result) => invariants.Evaluate(result);

    private static LoopInspection EmptyInspection() => new([], true, true, 0, false, null, null, false);

    private static LoopRunResult Result(
        LoopRunRequest request,
        int iterations,
        LoopTerminal terminal,
        string? stopReason,
        LoopInspection snapshot,
        string? blockReason,
        int contextChars) =>
        new(
            terminal,
            stopReason,
            iterations,
            snapshot.PermissionRuleIds,
            snapshot.PermissionsDrained,
            snapshot.PromptGateReleased,
            snapshot.TranscriptEvents,
            contextChars,
            snapshot.CompletedTask,
            blockReason,
            request.Seed);
}
