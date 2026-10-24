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
    IHostConnectionPresence? host = null)
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

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

    public async Task<ServerAutonomyDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var forgeEnabled = packages?.ListForges().Count > 0;
        var hostAuthorized = HostConnectionPresent;
        var agentAvailable = await scheduling.HasAvailableAgentAsync(cancellationToken);
        var enabled = autonomy.ServerEnabled;
        return new ServerAutonomyDto(enabled, [
            Item(AutonomyPreconditions.ForgeCapabilityEnabled, forgeEnabled),
            Item(AutonomyPreconditions.HostConnectionAuthorized, hostAuthorized),
            Item(AutonomyPreconditions.AgentAvailable, agentAvailable),
            Item(AutonomyPreconditions.AutonomyEnabled, enabled)
        ]);
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
                AssignedIssue task;
                try
                {
                    var next = await NextTaskAsync(workspaceId, current, completed, cancellationToken);
                    if (next is null)
                    {
                        Record(workspaceId, "idle", null, LoopTerminal.Idle, "queue_empty", null, []);
                        return;
                    }

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

                Record(workspaceId, "running", task, null, null, null, []);
                var policy = new DeclaredPermissionPolicy(current.PolicyRules);
                var prompt = current.Role is null
                    ? $"Work on issue #{task.Number}: {task.Title}\n\n{task.Body}"
                    : $"{current.Role.Prompt}\n\nWork on issue #{task.Number}: {task.Title}\n\n{task.Body}";
                var result = await loop.RunAsync(
                    new LoopRunRequest(workspaceId, prompt, 32, policy, 200_000, StandingPolicyReplies: current.StandingPolicyReplies),
                    cancellationToken);
                if (result.Terminal is not LoopTerminal.Completed)
                {
                    if (cancellationToken.IsCancellationRequested && settings.Get(workspaceId).Loop?.StopReason is "restarted")
                        return;
                    Record(workspaceId, "blocked", task, result.Terminal, result.StopReason, result.BlockReason, result.PermissionRuleIds);
                    return;
                }

                completed.Add(TaskKey(current.Role?.Trigger, task.Id));
                await DrainCompletedAsync(workspaceId, current, completed, cancellationToken);
                if (current.Role is not null && !progress.Satisfied(workspaceId, current.Role.Termination))
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
        AgentWorkspaceSettings current,
        HashSet<string> completed,
        CancellationToken cancellationToken)
    {
        if (current.Role?.Trigger == RoleTriggerKind.ProposalAwaitingReview)
        {
            var queue = await proposals.ListAwaitingAsync(workspaceId, cancellationToken);
            var proposal = queue.FirstOrDefault(item => !completed.Contains(TaskKey(RoleTriggerKind.ProposalAwaitingReview, item.Id)));
            return proposal is null ? null : ToIssue(proposal);
        }

        var assigned = await issues.ListAssignedAsync(workspaceId, current.QueueOrder, cancellationToken);
        return assigned.FirstOrDefault(item => !completed.Contains(TaskKey(RoleTriggerKind.IssueAssigned, item.Id)));
    }

    private async Task DrainCompletedAsync(
        string workspaceId,
        AgentWorkspaceSettings current,
        HashSet<string> completed,
        CancellationToken cancellationToken)
    {
        if (current.Role?.Trigger == RoleTriggerKind.ProposalAwaitingReview)
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
        new(proposal.Id, 0, proposal.Title, proposal.Body, proposal.CreatedAt, proposal.CreatedAt, proposal.Url);

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
