using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Agents.Services;

public sealed class AutonomousWorkspaceService(
    IAutonomySettings autonomy,
    IAgentWorkspaceSettingsRepository settings,
    LoopPreconditionProvider preconditions,
    IAssignedIssueQueue issues,
    IAssignedProposalQueue proposals,
    IRoleProgress progress,
    AgentSchedulingService scheduling,
    AutonomousLoopService loop,
    WorkspaceQueryController workspaces,
    IRoleToolFilter roleTools,
    IEnabledCapabilityPackages? packages = null,
    IHostConnectionPresence? host = null,
    ITaskPipeline? pipeline = null,
    Func<CancellationToken, Task>? waitForWork = null,
    IHostReviewSync? hostReviews = null)
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);
    private readonly Func<CancellationToken, Task> _waitForWork = waitForWork ?? (token => Task.Delay(TimeSpan.FromSeconds(30), token));

    public AutonomousWorkspaceService ReconcileAfterRestart()
    {
        var running = _runs.Keys.ToArray();
        foreach (var run in _runs.Values)
            run.Cancel();
        _runs.Clear();
        var persisted = settings.List().Where(item => item.AutonomyActive).Select(item => item.WorkspaceId);
        foreach (var workspaceId in running.Concat(persisted).Distinct(StringComparer.Ordinal))
        {
            var item = settings.Get(workspaceId);
            settings.Save(item with
            {
                AutonomyActive = false,
                Loop = new LoopStatusSnapshot(
                    false,
                    "blocked",
                    item.Loop?.CurrentTask,
                    LoopTerminal.Failed,
                    "restarted",
                    "The Server restarted mid-task. Re-activate after preconditions hold.",
                    item.Loop?.PermissionRuleIds ?? [])
            });
        }

        return this;
    }

    public bool ServerEnabled => autonomy.ServerEnabled;

    public bool HostConnectionPresent =>
        host?.IsPresent() ?? settings.List().Any(item => item.HostConnectionAuthorized);

    public Task<ServerAutonomyDto> GetStatusAsync(CancellationToken cancellationToken) =>
        GetStatusAsync(null, cancellationToken);

    public async Task<ServerAutonomyDto> GetStatusAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        var forgeEnabled = packages?.ListForges().Count > 0;
        var hostAuthorized = HostConnectionPresent;
        var agentAvailable = await scheduling.HasAvailableAgentAsync(cancellationToken);
        var enabled = autonomy.ServerEnabled;
        var items = new List<AutonomyPreconditionDto> {
            Item(AutonomyPreconditions.ForgeCapabilityEnabled, forgeEnabled),
            Item(AutonomyPreconditions.HostConnectionAuthorized, hostAuthorized),
            Item(AutonomyPreconditions.AgentAvailable, agentAvailable),
            Item(AutonomyPreconditions.AutonomyEnabled, enabled)
        };
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            var workspaceItems = await preconditions.StatusAsync(workspaceId, cancellationToken);
            var workspaceHost = workspaceItems.FirstOrDefault(item => item.Id == AutonomyPreconditions.HostConnectionAuthorized);
            if (workspaceHost is not null)
                items[1] = workspaceHost;
            items.AddRange(workspaceItems.Where(item => item.Id != AutonomyPreconditions.HostConnectionAuthorized));
        }
        return new ServerAutonomyDto(enabled, items);
    }

    public bool TryEnableServer(out string? refusal) => autonomy.TryEnable(out refusal);

    public void DisableServer() => autonomy.Disable();

    public Task<AgentActionResult> SetHostConnectionAsync(string workspaceId, bool authorized)
    {
        if (workspaces.GetById(workspaceId) is null)
            return Task.FromResult(AgentActionResult.NotFound());
        var current = settings.Get(workspaceId);
        settings.Save(current with { HostConnectionAuthorized = authorized });
        return Task.FromResult(AgentActionResult.Success());
    }

    public Task<AgentActionResult> SetPolicyAsync(string workspaceId, IReadOnlyList<PermissionRule> rules)
    {
        if (workspaces.GetById(workspaceId) is null)
            return Task.FromResult(AgentActionResult.NotFound());
        var current = settings.Get(workspaceId);
        var grants = TeamMemberGrants.For(current.Member);
        var exceeded = rules.Select(grants.Exceeds).FirstOrDefault(reason => reason is not null);
        if (exceeded is not null)
            return Task.FromResult(AgentActionResult.Failed(exceeded));
        settings.Save(current with { PolicyRules = rules });
        var policy = rules.Count == 0 ? null : new DeclaredPermissionPolicy(rules);
        scheduling.AttachPolicy(workspaceId, policy);
        return Task.FromResult(AgentActionResult.Success());
    }

    public Task<AgentActionResult> SetRoleAsync(string workspaceId, AgentRoleDefinition? role)
    {
        if (workspaces.GetById(workspaceId) is null)
            return Task.FromResult(AgentActionResult.NotFound());
        var current = settings.Get(workspaceId);
        settings.Save(current with { Role = role });
        return Task.FromResult(AgentActionResult.Success());
    }

    public Task<AgentActionResult> SetQueueOrderAsync(string workspaceId, AssignedIssueOrder order)
    {
        if (workspaces.GetById(workspaceId) is null)
            return Task.FromResult(AgentActionResult.NotFound());
        var current = settings.Get(workspaceId);
        settings.Save(current with { QueueOrder = order });
        return Task.FromResult(AgentActionResult.Success());
    }

    public async Task<AgentActionResult> ActivateAsync(string workspaceId, CancellationToken cancellationToken)
    {
        if (workspaces.GetById(workspaceId) is null)
            return AgentActionResult.NotFound();
        if (!autonomy.ServerEnabled)
            return AgentActionResult.Failed(AutonomyPreconditions.Refusal(AutonomyPreconditions.AutonomyEnabled));
        var unmet = await preconditions.UnmetAsync(workspaceId, cancellationToken);
        if (unmet is not null)
            return AgentActionResult.Failed(unmet);
        var current = settings.Get(workspaceId);
        settings.Save(current with
        {
            AutonomyActive = true,
            Loop = new LoopStatusSnapshot(true, "running", null, null, null, null, [])
        });
        var lifetime = new CancellationTokenSource();
        if (_runs.TryRemove(workspaceId, out var previous))
            previous.Cancel();
        _runs[workspaceId] = lifetime;
        _ = RunAsync(workspaceId, lifetime.Token);
        return AgentActionResult.Success();
    }

    public AgentActionResult Deactivate(string workspaceId)
    {
        if (workspaces.GetById(workspaceId) is null)
            return AgentActionResult.NotFound();
        if (_runs.TryRemove(workspaceId, out var run))
            run.Cancel();
        var current = settings.Get(workspaceId);
        settings.Save(current with
        {
            AutonomyActive = false,
            Loop = new LoopStatusSnapshot(false, "stopped", current.Loop?.CurrentTask, LoopTerminal.Cancelled, "cancelled", null, current.Loop?.PermissionRuleIds ?? [])
        });
        return AgentActionResult.Success();
    }

    public LoopStatusSnapshot Status(string workspaceId) =>
        settings.Get(workspaceId).Loop
        ?? new LoopStatusSnapshot(false, "stopped", null, null, null, null, []);

    public void RestrictRoleTools(McpServerOptions options, string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            return;
        var role = settings.Get(workspaceId).Role;
        if (role is null || role.Tools.Count == 0)
            return;
        roleTools.Restrict(options, role.Tools);
    }

    public IReadOnlyList<string> RoleTools(string workspaceId) =>
        settings.Get(workspaceId).Role?.Tools ?? [];

    public AgentRoleDefinition? Role(string workspaceId) => settings.Get(workspaceId).Role;

    public IReadOnlyList<PermissionRule> PolicyRules(string workspaceId) => settings.Get(workspaceId).PolicyRules;

    private async Task RunAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var completed = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var unmet = await preconditions.UnmetAsync(workspaceId, cancellationToken);
                if (unmet is not null)
                {
                    Record(workspaceId, "blocked", null, LoopTerminal.BlockedPrecondition, "precondition", unmet, []);
                    return;
                }

                var current = settings.Get(workspaceId);
                var role = TeamRoles.Effective(current);
                AssignedIssue task;
                try
                {
                    var next = await NextTaskAsync(workspaceId, role, completed, cancellationToken);
                    if (next is null)
                    {
                        Record(workspaceId, "idle", null, LoopTerminal.Idle, "queue_empty", null, []);
                        // A team works in turns: a reviewer has nothing until an author opens a
                        // proposal, and an author waits on its reviewers, so an active member waits
                        // for work rather than stopping.
                        if (pipeline is null)
                            return;
                        await _waitForWork(cancellationToken);
                        continue;
                    }

                    // Many workspaces draw from one queue, so the task is only ours once the queue
                    // says so. Losing the race means another workspace took it: choose again
                    // rather than open the same issue twice.
                    var taken = role.Trigger == RoleTriggerKind.ProposalAwaitingReview
                        ? await proposals.TryTakeAsync(workspaceId, next.Id, cancellationToken)
                        : await issues.TryHoldAsync(workspaceId, next.Id, cancellationToken);
                    if (!taken)
                        continue;

                    task = next;
                }
                catch (HttpRequestException exception)
                {
                    Record(workspaceId, "blocked", null, LoopTerminal.BlockedHostUnavailable, "host_unavailable", exception.Message, []);
                    return;
                }

                var bound = workspaces.GetById(workspaceId)?.BoundAgentModuleId;
                if (string.IsNullOrWhiteSpace(bound))
                {
                    Record(workspaceId, "blocked", task, LoopTerminal.BlockedPrecondition, "precondition", AutonomyPreconditions.Refusal(AutonomyPreconditions.BoundAgent), []);
                    return;
                }

                var scheduled = await scheduling.ScheduleAsync(workspaceId, bound, cancellationToken);
                if (scheduled.Error is not null)
                {
                    Record(workspaceId, "blocked", task, LoopTerminal.Failed, "schedule", scheduled.Error, []);
                    return;
                }

                if (pipeline is not null)
                {
                    var fresh = await scheduling.StartFreshSessionAsync(workspaceId, $"#{task.Number} {task.Title}", cancellationToken);
                    if (fresh.Error is not null)
                    {
                        Record(workspaceId, "blocked", task, LoopTerminal.Failed, "schedule", fresh.Error, []);
                        return;
                    }
                }

                Record(workspaceId, "running", task, null, null, null, []);
                progress.Reset(workspaceId);
                var policy = new DeclaredPermissionPolicy(current.PolicyRules);
                string prompt;
                if (pipeline is not null)
                {
                    var plan = await pipeline.PrepareAsync(workspaceId, role, task, cancellationToken);
                    if (plan.Prompt is null)
                    {
                        Record(workspaceId, "blocked", task, LoopTerminal.Failed, "prepare", plan.Refusal, []);
                        return;
                    }
                    prompt = plan.Prompt;
                }
                else
                {
                    var work = role.Trigger == RoleTriggerKind.ProposalAwaitingReview
                        ? $"Review proposal {task.Id}: {task.Title}\n{task.Url}\n\n{task.Body}"
                        : $"Work on issue #{task.Number}: {task.Title}\n\n{task.Body}";
                    prompt = current.Role is null ? work : $"{current.Role.Prompt}\n\n{work}";
                }
                var result = await loop.RunAsync(
                    new LoopRunRequest(workspaceId, prompt, 32, policy, 200_000, StandingPolicyReplies: current.StandingPolicyReplies,
                        ReplyCompletes: role.Trigger == RoleTriggerKind.ProposalAwaitingReview,
                        Submitted: pipeline is null ? null : () => progress.Satisfied(workspaceId, role.Termination)),
                    cancellationToken);
                if (result.Terminal is not LoopTerminal.Completed)
                {
                    if (cancellationToken.IsCancellationRequested && settings.Get(workspaceId).Loop?.StopReason is "restarted")
                        return;
                    Record(workspaceId, "blocked", task, result.Terminal, result.StopReason, result.BlockReason, result.PermissionRuleIds);
                    return;
                }

                if (pipeline is not null)
                {
                    // The queue carries the task on from here: an author's stays held while it is
                    // reviewed, and a reviewer is offered it again only for a new revision.
                    var outcome = await pipeline.FinishAsync(workspaceId, role, task, result, cancellationToken);
                    if (!outcome.Published)
                    {
                        Record(workspaceId, "blocked", task, LoopTerminal.BlockedAwaitingInput, outcome.StopReason, outcome.Cause, result.PermissionRuleIds);
                        return;
                    }
                }
                else
                {
                    completed.Add(TaskKey(role.Trigger, task.Id));
                    if (role.Trigger != RoleTriggerKind.ProposalAwaitingReview)
                        await issues.CompleteAsync(workspaceId, task.Id, cancellationToken);
                    await DrainCompletedAsync(workspaceId, role, completed, cancellationToken);
                }
                if ((current.Role is not null || pipeline is not null) && !progress.Satisfied(workspaceId, role.Termination))
                {
                    Record(workspaceId, "blocked", task, LoopTerminal.BlockedAwaitingInput, "termination", "blocked:termination", result.PermissionRuleIds);
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (settings.Get(workspaceId).Loop?.StopReason is "restarted")
                return;
            Record(workspaceId, "stopped", settings.Get(workspaceId).Loop?.CurrentTask, LoopTerminal.Cancelled, "cancelled", null, []);
        }
        finally
        {
            _runs.TryRemove(workspaceId, out _);
        }
    }

    private async Task<AssignedIssue?> NextTaskAsync(
        string workspaceId,
        AgentRoleDefinition role,
        HashSet<string> completed,
        CancellationToken cancellationToken)
    {
        var current = settings.Get(workspaceId);
        if (role.Trigger == RoleTriggerKind.ProposalAwaitingReview)
        {
            if (hostReviews is not null)
                await hostReviews.SyncAsync(workspaceId, cancellationToken);
            var queue = await proposals.ListAwaitingAsync(workspaceId, cancellationToken);
            var proposal = queue.FirstOrDefault(item => !completed.Contains(TaskKey(RoleTriggerKind.ProposalAwaitingReview, item.Id)));
            return proposal is null ? null : ToIssue(proposal);
        }

        var assigned = await issues.ListAssignedAsync(workspaceId, current.QueueOrder, cancellationToken);
        return assigned.FirstOrDefault(item => !completed.Contains(TaskKey(RoleTriggerKind.IssueAssigned, item.Id)));
    }

    private async Task DrainCompletedAsync(
        string workspaceId,
        AgentRoleDefinition role,
        HashSet<string> completed,
        CancellationToken cancellationToken)
    {
        var current = settings.Get(workspaceId);
        if (role.Trigger == RoleTriggerKind.ProposalAwaitingReview)
        {
            var queue = await proposals.ListAwaitingAsync(workspaceId, cancellationToken);
            proposals.Replace(workspaceId, [.. queue.Where(item => !completed.Contains(TaskKey(RoleTriggerKind.ProposalAwaitingReview, item.Id)))]);
            return;
        }

        var assigned = await issues.ListAssignedAsync(workspaceId, current.QueueOrder, cancellationToken);
        issues.Replace(workspaceId, [.. assigned.Where(item => !completed.Contains(TaskKey(RoleTriggerKind.IssueAssigned, item.Id)))]);
    }

    private static AutonomyPreconditionDto Item(string id, bool satisfied) =>
        new(id, satisfied, satisfied ? null : AutonomyPreconditions.Remedy(id));

    private static string TaskKey(RoleTriggerKind? trigger, string id) =>
        trigger == RoleTriggerKind.ProposalAwaitingReview ? $"proposal:{id}" : $"issue:{id}";

    private static AssignedIssue ToIssue(AssignedProposal proposal) =>
        new(proposal.Id, proposal.Number, proposal.Title, proposal.Body, proposal.CreatedAt, proposal.CreatedAt, proposal.Url);

    private void Record(
        string workspaceId,
        string phase,
        AssignedIssue? task,
        LoopTerminal? terminal,
        string? stopReason,
        string? blockReason,
        IReadOnlyList<string> rules)
    {
        var current = settings.Get(workspaceId);
        if (current.Loop?.StopReason is "restarted")
            return;
        var active = phase is "running" or "idle";
        settings.Save(current with
        {
            AutonomyActive = active && terminal is null or LoopTerminal.Idle,
            Loop = new LoopStatusSnapshot(active, phase, task, terminal, stopReason, blockReason, rules)
        });
    }
}
