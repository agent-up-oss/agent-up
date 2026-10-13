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
        try
        {
            for (var iteration = 1; iteration <= request.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var message = iteration == 1
                    ? request.Task
                    : "Continue the task. Call complete_task when it is done.";
                context += message.Length;
                var action = await scheduling.PromptWaitAsync(request.WorkspaceId, message, cancellationToken);
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

    private static LoopInspection EmptyInspection() => new([], true, true, 0, false, null, null);

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
