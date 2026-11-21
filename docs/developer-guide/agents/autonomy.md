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

Workspace-scoped rules cover worktree read, worktree write, execute, and network. The default for an unmatched request is refuse. A scoped execute rule matches the command the agent names in the tool call's `rawInput`, filled from the tool call it announced just before when the request itself omits it (OpenCode asks with the title `bash` and no command). A refused request is logged and carried on its `"permission_decision"` event. The Server auto-resolves a matching `session/request_permission` and records the rule id on the loop status. An unmatched request stops the loop as `blocked:policy`. A turn that makes no tool call is answered from standing policy a bounded number of times, then stops as `blocked:awaiting-input`.

An empty rule list is byte-identical to today's human path: the client still decides permissions. Autonomy activation requires at least one rule. Mobile's **Configure permissions** action edits the four broad allow rules; anything not selected remains blocked.

## Unattended loop

Autonomy is a Server-level switch, off by default. Enablement is stored in `autonomy-settings.json`; `AGENTUP_AUTONOMY_ENABLED` still forces it on. `GET /api/autonomy` returns the Server-wide `{ enabled, preconditions }`; `GET .../agent/autonomy` adds the workspace activation checks from the same provider used by `PUT .../agent/autonomy`. `PUT /api/autonomy` still requires an authorized host connection. Desktop and Mobile offer Enable autonomy once forge and host connection are ready, even while agent availability remains unmet; Activate appears only when the complete workspace-aware chain is satisfied. They render that chain and Activate/Cancel, and assert `LoopTerminal`, not transcript prose. The checklist and its actions live in settings in every shell: the classic workspace Settings tab on both clients, and Settings · Chat, one tab per workspace, in the command center. The Desktop Agent tab and Mobile Agents keep one summary line that opens Settings; chat is only the conversation. Drive the loop with `test-agent-scripted`. Each workspace then activates separately. Preconditions are checked at activate and at every task boundary:

- a bound agent that declares permission requests
- a non-empty policy
- an authorized host connection
- a git branch with no unpushed commits
- no commit-queue edit session
- no unassigned working-tree changes

The queue is the tasks somebody picked, shared by every autonomous workspace on the Server and held in the order they were arranged. An issue being open or assigned on the host puts nothing in it: `GET /api/workspaces/{workspaceId}/forge/issues` lists what the repository has open, and `POST /api/tasks` picks one. Agents work the queue and only the queue.

A workspace is offered its own held task first and nothing else; failing that, the free tasks for its own `owner/repository` that are either untargeted or targeted at it. Taking a task is one locked step, so of two workspaces reaching for the same issue exactly one gets it and the other chooses again. A hold is sticky and persisted: a run that blocks, fails, or is cut short by a restart keeps its task, so the workspace that started it is the one that carries it on. `POST /api/tasks/{owner}/{repository}/{id}/release` is the deliberate way to move work off a stuck workspace; the clients offer it as Stop on the task, which is in progress rather than in the queue while it is held. Without the team pipeline a completed task leaves the queue and an empty queue stops as `Idle`; with it (the Server default) the task moves through stages instead, and an active member with nothing to do waits as `Idle` and looks again every 15 seconds rather than stopping. A rate-limit signal backs off and retries before becoming `BlockedRateLimited`. A Server restart cancels in-process runs and marks a persisted active loop `Failed` / `restarted`.

A task is `Completed` when the agent says so (`completed: true` on the prompt result, which only the test agents send) or, for real ACP agents, when a turn that follows tool use ends without another tool call: stopping is how a coding CLI hands back. A reviewer's task also completes on a reply alone, because a review is an answer rather than a change. The loop's prompt timeout is a silence window, not a turn budget, and a turn that repeats one failing tool call five times stops as `Failed` / `stuck`. Each task attempt starts a fresh ACP session on the running agent process, so one attempt's conversation does not steer the next.

Every run ends in one typed terminal: `Completed`, `EndTurn`, `Cancelled`, `MaxIterations`, `BlockedPolicy`, `BlockedAwaitingInput`, `BlockedRateLimited`, `BlockedPrecondition`, `BlockedHostUnavailable`, `Failed`, or `Idle`. Desktop and Mobile render `session.loop` and accept a prompt while a turn is running as steering. Steering is advisory: it is queued and delivered at the next turn boundary; it does not stop the loop.

## Team pipeline

A member with no declared role plays its kind's role (`TeamRoles`): an author is triggered by `IssueAssigned` and terminates on `ProposalOpened`; dev and PM reviewers are triggered by `ProposalAwaitingReview` and terminate on `ReviewSubmitted`. `TaskPipelineService` does the Git and host work around each turn, so agents only change files and say what they think:

1. **Author, before.** A fresh task starts from its base branch (fetched and reset when the clone is on an earlier task branch). The Server starts `agent-up/issue-<n>` there and pushes it, assigns the issue to the author's host account, and moves the task to `implementing`. The prompt leads with the issue, then the role. A task sent back also carries what the host said about the revision: each finding as `path:line (reviewer): comment`, every failing check with its link, and the comments of anyone but the author.
2. **Author, after.** The Server commits every change to `agent-up/issue-<n>` under the member's name, pushes it with the workspace's own forge credential, and opens the proposal (`Closes #n`) or, on a later revision, comments on it. The task moves to `inReview` with a new revision number. A turn that changed nothing blocks as `"nothing_to_propose"`; the agent's claim that it committed is not evidence. A commit an earlier attempt could not push is published on the next one.
3. **Reviewer, before.** The Server requests the reviewer on the proposal with the author's credential, again for every revision. The reviewer fetches the task branch and its base with its own credential and checks out exactly that revision. A dev reviewer's prompt carries the diff inline, cut at 12,000 characters. A PM reviewer's prompt carries the issue and the proposal, never the code. A PM reviewer may operate the application but not start its workspace, so the Server restarts the workspace on that revision before the turn, opens the first HTTP application in the reviewer's own browser once the workspace is ready, and puts each address in the prompt; a revision that does not start blocks the review rather than reviewing the old one.
4. **Reviewer, after.** The reply's last `VERDICT: APPROVE` or `VERDICT: REQUEST_CHANGES` (also written as "the VERDICT is APPROVE") becomes a review on the proposal under the reviewer's account. A reply without one is not posted and blocks as `"no_verdict"`, so the review stays owed. Once the review is in, the Server stops the reviewer's workspace.

A review is triggered by a push. Each revision records the commit its branch pointed at, and before a reviewer is offered work it fetches every proposed branch. A head that moved, whether the author's loop or a person pushed it, opens the next revision in `inReview`, so no verdict on earlier code decides it.

A reviewer's turn completes only on a reply that carries its verdict. A reply without one is answered with a request for exactly that line rather than a generic continue, which a small model reads as a reason to call more tools.

Reviews come from two queues, dev and PM. The host only knows that a number of approvals is required; the Server infers which kinds are owed from the reviewer kinds the team has on the repository (`TaskReviewRequirementService`) and from the reviews already given. The first reviewer of a kind to take a task holds that review (`devReviewerWorkspaceId`, `pmReviewerWorkspaceId`) for every later revision: no other reviewer of that kind is offered it, so a push goes back to the reviewer that already knows the change.

Any request for changes on the open revision moves the task to `changesRequested` and back to its author, whose next prompt carries those reviews; a review of the same revision that lands afterwards is still kept for the author. A failing check on the commit under review does the same: the author's loop reads the proposal's checks while it waits, and only the latest result of each check on that commit counts. The task is `approved` only when the reviewer holding each required kind approved the same revision; a comment never approves. An author whose task is in review is offered nothing and waits; an approved task releases its author. Merging stays with the host. `GET /api/tasks` returns each task's `stage`, `proposal` (number, URL, branch, base, revision, head), `reviews` (each with its `kind`), the two reviewer ids, and `needsReview`, the kinds the open revision still waits on.

The command center's Tasks board has three tabs over that one queue: **Issues** (in progress, queued, open issues), **Dev review**, and **PM review**. Each review tab sorts the proposed tasks for its kind from `needsReview`, the reviewer id, and the reviews of the open revision: needs a reviewer, under review, reviewed. A task back with its author that this kind has not reviewed is left out until the next push. Review rows have no action; reviewers take reviews themselves.

The Server pushes only `agent-up/` branches (`ForgeController.TaskBranchPrefix`). Credentials travel to Git as an HTTP header through `GIT_CONFIG_*` environment variables, never on a command line, in the clone's config, or in the remote URL.

## Roles

A role is a prompt prefix plus an MCP tool allowlist, a trigger (`IssueAssigned` or `ProposalAwaitingReview`), and a termination (`ProposalOpened` or `ReviewSubmitted`). The allowlist filters advertised MCP tools through the same session configuration path that permission claims already use. An empty allowlist keeps the default per-endpoint toolset. `IssueAssigned` draws from the picked task queue; `ProposalAwaitingReview` draws the picked tasks whose open revision still needs this reviewer's kind of review and whose review of that kind is free or already this reviewer's. After `Completed`, an unmet termination stops the loop as `blocked:awaiting-input` with stop reason `termination`.

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
