using System.Collections.Concurrent;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Workspaces.Controllers;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Agents.Services;

public sealed class AutonomousWorkspaceService(
    IAutonomySettings autonomy,
    IAgentWorkspaceSettingsRepository settings,
    LoopPreconditionProvider preconditions,
    IAssignedIssueQueue issues,
    AgentSchedulingService scheduling,
    AutonomousLoopService loop,
    WorkspaceQueryController workspaces,
    IRoleToolFilter roleTools)
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

    public AutonomousWorkspaceService ReconcileAfterRestart()
    {
        foreach (var item in settings.List().Where(item => item.AutonomyActive))
        {
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

    public bool HostConnectionPresent => settings.List().Any(item => item.HostConnectionAuthorized);

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
            return AgentActionResult.Failed("Autonomy is not enabled on this Server.");
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
                IReadOnlyList<AssignedIssue> queue;
                try
                {
                    queue = await issues.ListAssignedAsync(workspaceId, current.QueueOrder, cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    Record(workspaceId, "blocked", null, LoopTerminal.BlockedHostUnavailable, "host_unavailable", exception.Message, []);
                    return;
                }

                if (queue.Count == 0)
                {
                    Record(workspaceId, "idle", null, LoopTerminal.Idle, "queue_empty", null, []);
                    return;
                }

                var task = queue[0];
                var bound = workspaces.GetById(workspaceId)?.BoundAgentModuleId;
                if (string.IsNullOrWhiteSpace(bound))
                {
                    Record(workspaceId, "blocked", task, LoopTerminal.BlockedPrecondition, "precondition", "An agent is not bound to the workspace.", []);
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
                    Record(workspaceId, "blocked", task, result.Terminal, result.StopReason, result.BlockReason, result.PermissionRuleIds);
                    return;
                }

                issues.Replace(workspaceId, [.. queue.Skip(1)]);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Record(workspaceId, "stopped", settings.Get(workspaceId).Loop?.CurrentTask, LoopTerminal.Cancelled, "cancelled", null, []);
        }
        finally
        {
            _runs.TryRemove(workspaceId, out _);
        }
    }

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
        var active = phase is "running" or "idle";
        settings.Save(current with
        {
            AutonomyActive = active && terminal is null or LoopTerminal.Idle,
            Loop = new LoopStatusSnapshot(active, phase, task, terminal, stopReason, blockReason, rules)
        });
    }
}
