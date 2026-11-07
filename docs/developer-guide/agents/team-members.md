---
title: Team members
---

# Team members

<DocWhat>
One Server hosts many workspaces, and each workspace is one team member with its own checkout and its own orchestration. The member kind fixes what that workspace's agent may read, write, drive, and raise. This page is the contract for that matrix and for the in-process agent PM reviewers run on.
</DocWhat>

<DocFacts>
<DocFact label="Read">{'GET /api/workspaces/{id}/agent/member'}</DocFact>
<DocFact label="Change">{'PUT /api/workspaces/{id}/agent/member'} with `{ "kind": "Author" | "DevReviewer" | "PmReviewer" }`</DocFact>
<DocFact label="Default">`Author`</DocFact>
<DocFact label="Definition">`TeamMemberGrants` in the Server Agents slice</DocFact>
</DocFacts>

## Matrix

| Kind | Reads repository | Writes repository | Drives browser | Submits reviews | Fixture |
|---|---|---|---|---|---|
| `Author` | yes | yes | yes | no | ACP coding CLI |
| `DevReviewer` | yes | no | no | yes | ACP coding CLI |
| `PmReviewer` | no | no | yes | yes | in-process |

Authors and dev reviewers work on code, so they run an ACP coding CLI against their checkout: Claude, Codex, or Cursor today. PM reviewers have nothing to do with code and skip that layer.

A review raises threads another member must address, so only reviewers submit one. Authors still post plain proposal comments, and address threads through `enqueue_review_fix_commit`.

## Enforcement

Every enforcement point reads `TeamMemberGrants`; none restates the rule.

- **Binding.** Binding, scheduling, or resuming an agent of the other fixture is refused with a typed error naming the member kind.
- **Permission policy.** `PUT .../agent/policy` refuses an allow rule outside the grant. A member that cannot write may allow only named `Execute` commands, because an unscoped command can write the worktree. A PM may allow only `Network`.
- **Forge reviews.** `ForgeService.SubmitReviewAsync` refuses an `Author` workspace; `POST .../forge/reviews` answers `403`. REST, an in-process agent, and any later caller meet the same check.
- **MCP.** A session pinned to a workspace by a `workspace` claim is narrowed to the member's operation permissions after the token, workspace, and role narrowings. Each narrowing only removes.
- **Changing kind.** Refused while autonomy is active, while the bound agent is the wrong fixture, or while a stored policy rule exceeds the new grant.

## In-process agent

The `ollama` agent runs inside the Server against an Ollama-compatible `/api/chat` endpoint with tool calling. It is listed only when `AGENTUP_OLLAMA_MODEL` is set. `AGENTUP_OLLAMA_URL` defaults to `http://127.0.0.1:11434`. Use a vision-capable model so screenshots carry information.

It answers ACP v1 (`initialize`, `session/new`, `session/prompt`, `session/cancel`), so scheduling, the event stream, permission decisions, the loop kernel, and both clients treat it like any other agent. It has no process, no filesystem, and no shell.

Its tools come from the Server's own `/mcp/browser` and `/mcp/orchestration` over loopback, through `LoopbackMcpClientProvider`. `AGENTUP_INPROCESS_MCP_URL` overrides the dialed listener. `InProcessAgentToolService` narrows them three ways, each only removing:

1. Tools that name their workspace through `workspaceId`. That parameter and `worktreePath` are removed from the schema the model sees and supplied by the host.
2. Tools whose `McpToolPermissionMap` permission the member grant holds.
3. The one workspace the agent belongs to.

Two host tools sit beside them; they are not MCP tools. **Submit review** goes through the forge review check and records `ReviewSubmitted` for the role termination. **Complete task** ends the loop task.

Every tool call except Complete task is first sent as ACP `session/request_permission` with tool kind `fetch`, which `DeclaredPermissionPolicy` classifies as `Network`. A PM's autonomy policy is therefore one `Network` allow rule, and without a policy a person approves each call.

## PM review loop

A PM reviewer is an autonomous workspace with role trigger `ProposalAwaitingReview` and termination `ReviewSubmitted`. Its prompt names the proposal id, title, and URL. An unmet termination after `Completed` still stops as `blocked:awaiting-input`.

```json
{
  "prompt": "Review the running product against the proposal. Report what a user would notice.",
  "tools": [],
  "trigger": "ProposalAwaitingReview",
  "termination": "ReviewSubmitted"
}
```

## Tests

`TeamMemberGrantsTests` holds the matrix. `AutonomyHttpTests` covers the member routes, fixture refusal, and policy refusal. `ForgeServiceTests` covers the review gate. Provider tests cover the chat request shape, loopback MCP sessions, schema pinning, and the ACP emulation with a scripted model.

<DocNext href="/developer-guide/agents/sign-in" title="Sign-in">
Subscription sign-in transports for ACP coding CLIs.
</DocNext>
