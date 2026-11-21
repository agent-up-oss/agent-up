using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Thin Server loop kernel: iterate prompts against the scheduled ACP session, resolve every
/// permission through an attached policy, and stop in one typed terminal. Rate-limit signals
/// back off and retry before becoming a blocked state.
/// </summary>
public sealed class AutonomousLoopService(
    AgentSchedulingService scheduling,
    LoopInvariantGuard invariants,
    ILoopBackoff backoff)
{
    /// <summary>
    /// What a reviewer is told when a turn ends without a verdict. A generic "continue" reads as
    /// "do more", and a small model then calls tools it does not need; this asks for the one
    /// thing still missing.
    /// </summary>
    public const string VerdictNudge =
        "Finish your review now without running anything else. Reply with what is good, what must change, " +
        "and a last line that is exactly VERDICT: APPROVE or VERDICT: REQUEST_CHANGES.";

    private static bool GivesVerdict(string? reply) =>
        !string.IsNullOrWhiteSpace(reply) && ReviewVerdictText.Read(reply).Verdict != TaskReviewVerdict.Comment;

    public async Task<LoopRunResult> RunAsync(LoopRunRequest request, CancellationToken cancellationToken)
    {
        if (!scheduling.AttachPolicy(request.WorkspaceId, request.Policy))
            return Result(request, 0, LoopTerminal.Failed, "not-found", EmptyInspection(), "workspace agent is not scheduled", 0);

        scheduling.BeginLoopTask(request.WorkspaceId);
        var context = request.Task.Length;
        var noProgress = 0;
        var worked = false;
        var rateLimitedAttempts = 0;
        try
        {
            for (var iteration = 1; iteration <= request.MaxIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var message = iteration == 1
                    ? request.Task
                    : request.ReplyCompletes
                        ? VerdictNudge
                    : noProgress > 0
                        ? "Continue under the standing permission policy. Do not wait for confirmation. When the task is done, reply with a short summary and make no further changes."
                        : "Continue the task. When it is done, reply with a short summary and make no further changes.";
                context += message.Length;
                var (action, stuck) = await TurnAsync(request, message, cancellationToken);
                if (action is null)
                {
                    await scheduling.CancelAsync(request.WorkspaceId, CancellationToken.None);
                    var hung = scheduling.InspectLoop(request.WorkspaceId);
                    return stuck
                        ? Result(request, iteration, LoopTerminal.Failed, "stuck", hung, $"The agent repeated the same failing tool call {request.RepeatedFailureLimit} times.", context)
                        : Result(request, iteration, LoopTerminal.Failed, "unresponsive", hung, "The agent stopped writing but held the session open.", context);
                }
                var snapshot = scheduling.InspectLoop(request.WorkspaceId);
                if (context > request.ContextCharLimit)
                    return Result(request, iteration, LoopTerminal.Failed, "context_limit", snapshot, "context truncated", context);
                if (!string.IsNullOrWhiteSpace(snapshot.BlockReason))
                    return Result(request, iteration, LoopTerminal.BlockedPolicy, snapshot.StopReason ?? "end_turn", snapshot, snapshot.BlockReason, context);
                if (action.Error is not null)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return Result(request, iteration, LoopTerminal.Cancelled, "cancelled", snapshot, null, context);
                    return Result(request, iteration, LoopTerminal.Failed, snapshot.StopReason ?? "error", snapshot, action.Error, context);
                }
                if (snapshot.CompletedTask)
                    return Result(request, iteration, LoopTerminal.Completed, snapshot.StopReason ?? "end_turn", snapshot, null, context);
                if (string.Equals(snapshot.StopReason, "cancelled", StringComparison.Ordinal))
                    return Result(request, iteration, LoopTerminal.Cancelled, "cancelled", snapshot, null, context);
                if (string.Equals(snapshot.StopReason, "rate_limited", StringComparison.OrdinalIgnoreCase))
                {
                    if (rateLimitedAttempts < request.RateLimitRetries)
                    {
                        rateLimitedAttempts++;
                        await backoff.DelayAsync(rateLimitedAttempts, cancellationToken);
                        continue;
                    }

                    return Result(request, iteration, LoopTerminal.BlockedRateLimited, "rate_limited", snapshot, "blocked:rate-limited", context);
                }
                // A real ACP agent has no way to say "done" except by stopping: a turn that uses no
                // tool after the task has done work is the agent handing the result back.
                // A reviewer's task is its verdict, so only a reply that gives one hands it back.
                if (!snapshot.SawToolCall && worked && !request.ReplyCompletes
                    && string.Equals(snapshot.StopReason, "end_turn", StringComparison.Ordinal))
                    return Result(request, iteration, LoopTerminal.Completed, "settled", snapshot, null, context);
                // A review is an answer, not a change: a reviewer that replies with its verdict
                // has done its task; one that replies without it is asked for it.
                if (request.ReplyCompletes && !snapshot.SawToolCall && GivesVerdict(snapshot.LastReply)
                    && string.Equals(snapshot.StopReason, "end_turn", StringComparison.Ordinal))
                    return Result(request, iteration, LoopTerminal.Completed, "replied", snapshot, null, context);
                worked |= snapshot.SawToolCall;
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

    /// <summary>
    /// Runs one prompt and waits for it. <see cref="LoopRunRequest.PromptTimeoutMs"/> is a window of
    /// silence, not a budget for the turn: a real agent can work for many minutes, and only one
    /// that has emitted nothing for that long while holding the session is unresponsive. A turn that
    /// keeps talking is still stopped once it repeats one failing tool call
    /// <see cref="LoopRunRequest.RepeatedFailureLimit"/> times, because that never ends on its own.
    /// A null action means the turn was stopped; <c>Stuck</c> says which of the two stopped it.
    /// </summary>
    private async Task<(AgentActionResult? Action, bool Stuck)> TurnAsync(LoopRunRequest request, string message, CancellationToken cancellationToken)
    {
        var window = Math.Max(1, request.PromptTimeoutMs);
        var slice = TimeSpan.FromMilliseconds(Math.Clamp(window / 5, 5, 1000));
        var turn = scheduling.PromptWaitAsync(request.WorkspaceId, message, cancellationToken);
        var heard = scheduling.InspectLoop(request.WorkspaceId).TranscriptEvents;
        var quietSince = Environment.TickCount64;
        while (!turn.IsCompleted)
        {
            await Task.WhenAny(turn, Task.Delay(slice, CancellationToken.None));
            cancellationToken.ThrowIfCancellationRequested();
            if (turn.IsCompleted)
                break;
            var inspection = scheduling.InspectLoop(request.WorkspaceId);
            if (inspection.RepeatedFailures >= request.RepeatedFailureLimit)
                return (null, true);
            var now = inspection.TranscriptEvents;
            if (now != heard)
            {
                heard = now;
                quietSince = Environment.TickCount64;
            }
            else if (Environment.TickCount64 - quietSince >= window)
                return (null, false);
        }

        return (await turn, false);
    }

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
            request.Seed,
            string.IsNullOrWhiteSpace(snapshot.LastReply) ? null : snapshot.LastReply);
}
