---
title: Autonomy
---

# Autonomy

<DocWhat>
A workspace can bind one agent, attach a permission policy, and drain assigned host issues without a developer in the prompt loop. Interactive chat is unchanged until those pieces are present. This page is the contract for that unattended path.
</DocWhat>

<DocFacts>
<DocFact label="Server enable">AGENTUP_AUTONOMY_ENABLED (wins) or persisted PUT /api/autonomy</DocFact>
<DocFact label="Bind">{'PUT /api/workspaces/{id}/agent/binding'}</DocFact>
<DocFact label="Activate">{'PUT /api/workspaces/{id}/agent/autonomy'}</DocFact>
<DocFact label="Default">Off. No policy means the interactive permission path.</DocFact>
</DocFacts>

## Workspace-bound agent

`Workspace.BoundAgentModuleId` is the agent that owns the workspace. `PUT .../agent/binding` sets it. `DELETE .../agent/binding` clears it when the live session is stopped or idle. Binding a different module while a session is live is refused with a typed error that names that session.

An unbound workspace is not an error. Desktop hides the Agent-tab picker once a bind is present and shows the bound module instead. Mobile Agents shows only a created bound agent; otherwise its + action opens the enabled-module creation page. Mobile schedules from that page so a missing executable does not leave a new bind, and clears an existing bind that has no live agent before another choice. Saved session history stays keyed by workspace; a resume that names a different module than the bind is refused.

`IAgentCapability.SupportsPermissionRequests` is required before autonomy will activate for that bind. Codex, Cursor, and Claude declare it true.

## Permission policy

Workspace-scoped rules cover worktree read, worktree write, execute, and network. The default for an unmatched request is refuse. The Server auto-resolves a matching `session/request_permission` and records the rule id on the loop status. An unmatched request stops the loop as `blocked:policy`. A turn that makes no tool call is answered from standing policy a bounded number of times, then stops as `blocked:awaiting-input`.

An empty rule list is byte-identical to today's human path: the client still decides permissions. Autonomy activation requires at least one rule. Mobile's **Configure permissions** action edits the four broad allow rules; anything not selected remains blocked.

## Unattended loop

Autonomy is a Server-level switch, off by default. Enablement is stored in `autonomy-settings.json`; `AGENTUP_AUTONOMY_ENABLED` still forces it on. `GET /api/autonomy` returns the Server-wide `{ enabled, preconditions }`; `GET .../agent/autonomy` adds the workspace activation checks from the same provider used by `PUT .../agent/autonomy`. `PUT /api/autonomy` still requires an authorized host connection. Desktop and Mobile offer Enable autonomy once forge and host connection are ready, even while agent availability remains unmet; Activate appears only when the complete workspace-aware chain is satisfied. They render that chain and Activate/Cancel, and assert `LoopTerminal`, not transcript prose. Chat hides the checklist while subscription sign-in is in progress so the sign-in action stays on screen. Drive the loop with `test-agent-scripted`. Each workspace then activates separately. Preconditions are checked at activate and at every task boundary:

- a bound agent that declares permission requests
- a non-empty policy
- an authorized host connection
- a git branch with no unpushed commits
- no commit-queue edit session
- no unassigned working-tree changes

The queue is the tasks somebody picked, shared by every autonomous workspace on the Server and held in the order they were arranged. An issue being open or assigned on the host puts nothing in it: `GET /api/workspaces/{workspaceId}/forge/issues` lists what the repository has open, and `POST /api/tasks` picks one. Agents work the queue and only the queue.

A workspace is offered its own held task first and nothing else; failing that, the free tasks for its own `owner/repository` that are either untargeted or targeted at it. Taking a task is one locked step, so of two workspaces reaching for the same issue exactly one gets it and the other chooses again. A hold is sticky and persisted: a run that blocks, fails, or is cut short by a restart keeps its task, so the workspace that started it is the one that carries it on. `POST /api/tasks/{owner}/{repository}/{id}/release` is the deliberate way to move work off a stuck workspace; the clients offer it as Stop on the task, which is in progress rather than in the queue while it is held. A completed task leaves the queue. An empty queue stops as `Idle`. A rate-limit signal backs off and retries before becoming `BlockedRateLimited`. A Server restart cancels in-process runs and marks a persisted active loop `Failed` / `restarted`.

Every run ends in one typed terminal: `Completed`, `EndTurn`, `Cancelled`, `MaxIterations`, `BlockedPolicy`, `BlockedAwaitingInput`, `BlockedRateLimited`, `BlockedPrecondition`, `BlockedHostUnavailable`, `Failed`, or `Idle`. Desktop and Mobile render `session.loop` and accept a prompt while a turn is running as steering. Steering is advisory: it is queued and delivered at the next turn boundary; it does not stop the loop.

## Roles

A role is a prompt prefix plus an MCP tool allowlist, a trigger (`IssueAssigned` or `ProposalAwaitingReview`), and a termination (`ProposalOpened` or `ReviewSubmitted`). The allowlist filters advertised MCP tools through the same session configuration path that permission claims already use. An empty allowlist keeps the default per-endpoint toolset. `IssueAssigned` draws from the picked task queue; `ProposalAwaitingReview` drains the proposal queue. After `Completed`, an unmet termination stops the loop as `blocked:awaiting-input` with stop reason `termination`.

```json
{
  "prompt": "Review the proposed change. Do not edit the worktree.",
  "tools": ["get_commits_status", "plan_verification"],
  "trigger": "ProposalAwaitingReview",
  "termination": "ReviewSubmitted"
}
```

## Tests

`test-agent-scripted` is a declared-turn ACP agent. `loop-scripted` is a fast check over that agent plus Server `ScriptedLoop` Provider tests for every loop branch. `Category=LoopInference` still drives the kernel against CPU llama.cpp. Concurrency and restart live in the normal E2E tier (`AutonomousLoopIsolationTests`). `Category=LoopSoak` (`slow`, `ciOnly`) drains hundreds of scripted tasks and asserts bounded memory, handles, threads, and transcript size; CI runs it with `./.github/scripts/run-loop-soak.sh`.

<DocNext href="/developer-guide/agents/loop-coverage" title="Loop coverage">
CPU-inference invariants, the pinned GGUF, and the LoopInference job.
</DocNext>
