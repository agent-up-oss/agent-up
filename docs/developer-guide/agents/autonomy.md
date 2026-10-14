---
title: Autonomy
---

# Autonomy

A workspace can bind one agent, attach a permission policy, and drain assigned host issues without a developer in the prompt loop. Interactive chat is unchanged until those pieces are present. This page is the contract for that unattended path.

<DocFacts>
<DocFact label="Server enable">AGENTUP_AUTONOMY_ENABLED or PUT /api/autonomy</DocFact>
<DocFact label="Bind">{'PUT /api/workspaces/{id}/agent/binding'}</DocFact>
<DocFact label="Activate">{'PUT /api/workspaces/{id}/agent/autonomy'}</DocFact>
<DocFact label="Default">Off. No policy means the interactive permission path.</DocFact>
</DocFacts>

## Workspace-bound agent

`Workspace.BoundAgentModuleId` is the agent that owns the workspace. `PUT .../agent/binding` sets it. `DELETE .../agent/binding` clears it when the live session is stopped or idle. Binding a different module while a session is live is refused with a typed error that names that session.

An unbound workspace is not an error. Desktop hides the Agent-tab picker once a bind is present and shows the bound module instead. Mobile Agents does the same. Selecting an agent from the picker binds, then schedules. Saved session history stays keyed by workspace; a resume that names a different module than the bind is refused.

`IAgentCapability.SupportsPermissionRequests` is required before autonomy will activate for that bind. Codex, Cursor, and Claude declare it true.

## Permission policy

Workspace-scoped rules cover worktree read, worktree write, execute, and network. The default for an unmatched request is refuse. The Server auto-resolves a matching `session/request_permission` and records the rule id on the loop status. An unmatched request stops the loop as `blocked:policy`. A turn that makes no tool call is answered from standing policy a bounded number of times, then stops as `blocked:awaiting-input`.

An empty rule list is byte-identical to today's human path: the client still decides permissions. Autonomy activation requires at least one rule.

## Unattended loop

Autonomy is a Server-level switch, off by default, and it needs a host connection on at least one workspace before `PUT /api/autonomy` can enable it. Each workspace then activates separately. Preconditions are checked at activate and at every task boundary:

- a bound agent that declares permission requests
- a non-empty policy
- an authorized host connection
- a git branch with no unpushed commits
- no commit-queue edit session
- no unassigned working-tree changes

The queue is the host issues assigned to that agent, in a declared order (`created` by default). Memory is the in-process default; `GitHubAssignedIssueProvider` is the HTTP adapter. After a task completes as `Completed`, that issue is removed and the next runs. An empty queue stops as `Idle`. A Server restart does not silently resume: `ReconcileAfterRestart` marks a persisted active loop `Failed` / `restarted`.

Every run ends in one typed terminal: `Completed`, `EndTurn`, `Cancelled`, `MaxIterations`, `BlockedPolicy`, `BlockedAwaitingInput`, `BlockedRateLimited`, `BlockedPrecondition`, `BlockedHostUnavailable`, `Failed`, or `Idle`. Desktop and Mobile render `session.loop` and accept a prompt while a turn is running as steering. Steering is advisory: it is queued and delivered at the next turn boundary; it does not stop the loop.

## Roles

A role is a prompt prefix plus an MCP tool allowlist, a trigger (`IssueAssigned` or `ProposalAwaitingReview`), and a termination (`ProposalOpened` or `ReviewSubmitted`). The allowlist filters advertised MCP tools through the same session configuration path that permission claims already use. An empty allowlist keeps the default per-endpoint toolset.

## Tests

`test-agent-scripted` is a declared-turn ACP agent. `loop-scripted` is a fast check over `AgentUp.TestAgents.Tests` `Features/Scripted`. `Category=LoopInference` still drives the kernel against CPU llama.cpp. `Category=LoopSoak` (`slow`, `ciOnly`) runs three concurrent loops through hundreds of assigned issues and proves restart does not resume; CI runs it with `./.github/scripts/run-loop-soak.sh`.

<DocNext href="/developer-guide/agents/loop-coverage" title="Loop coverage">
CPU-inference invariants, the pinned GGUF, and the LoopInference job.
</DocNext>
