---
title: MCP
---

# MCP

The Server exposes separate MCP servers at `/mcp/orchestration`, `/mcp/browser`, `/mcp/audit`, and `/mcp/commits`. MCP is the primary automation interface for AI agents.

The CLI exists for human convenience. AI agents should use MCP directly.

The Server sends MCP initialization instructions that tell clients to use Agent-Up tools whenever a user asks to use Agent-Up, agent up, the Agent-Up server, or the Agent-Up workspace manager. User wording such as "deploy my app with Agent-Up", "run my app with Agent-Up", "start this workspace", "bring up the app", "serve this repo", or "open the app in Agent-Up" means the agent should call `start_workspace` with the absolute repository/worktree path. Agent-Up starts and manages local development environments; it does not deploy to cloud infrastructure.

The Orchestration MCP server exposes Streamable HTTP at `/mcp/orchestration` and legacy SSE compatibility at `/mcp/orchestration/sse` plus `/mcp/orchestration/message`. It owns workspace tools, workspace resources, and Agent-Up context resources.

The Commits MCP server exposes Streamable HTTP at `/mcp/commits` and legacy SSE compatibility at `/mcp/commits/sse` plus `/mcp/commits/message`. It owns only commit queue tools and exposes no workspace resources.

The Browser MCP server exposes Streamable HTTP at `/mcp/browser` and legacy SSE compatibility at `/mcp/browser/sse` plus `/mcp/browser/message`. It owns browser navigation, inspection, interaction, wait, and screenshot tools.

The Audit MCP server exposes Streamable HTTP at `/mcp/audit` and legacy SSE compatibility at `/mcp/audit/sse` plus `/mcp/audit/message`. It owns durable audit history queries and Server-managed artifact loading.

This is a breaking endpoint split. Clients must connect to the specific MCP server they need instead of the former shared `/mcp` endpoint. MCP is a protocol surface, not a feature slice: tools and resources are thin controller-layer adapters owned by the feature slice whose capability they expose. Cross-capability workspace and context tools live in the `Orchestration` slice.

## Resources

Initial `/mcp/orchestration` resources:

```text
agent-up://context
agent-up://agent-up-json
agent-up://workspaces
agent-up://workspaces/{id}
```

Resources expose Agent-Up context, the declarative `agent-up.json` format, and current workspace state.

## Tools

Initial `/mcp/orchestration` tools:

- `start_workspace`: registers or updates a workspace from its `agent-up.json`, then starts it. Use it for requests to deploy, run, start, launch, serve, bring up, or open an app/workspace with Agent-Up.
- `stop_workspace`: stops a registered workspace by workspace ID or worktree path.
- `get_workspace_status`: returns a selected workspace status or all workspace statuses.
- `list_workspaces`: lists registered workspaces.
- `get_agent_up_json_format`: returns the current declarative configuration format.
- `get_agent_up_context`: returns concise Agent-Up operating rules for AI agents.

Initial `/mcp/commits` tools:

- `enqueue_commit`: saves a vertical-slice patch in the commit queue and restores the tracked files to their pre-change state for `agentup commits next`.
- `enqueue_review_fix_commit`: saves one review issue violation fix with a required `reviewIssueId`; do not combine multiple review issues in one entry.
- `get_commits_status`: returns queued entries, unassigned modified files, any active commit edit session, and active Git operation state.
- `guard_commits`: blocks new work while queued entries, active edit sessions, staged changes, unassigned modified files, or active Git merge/rebase/cherry-pick/revert/bisect operations exist.
- `get_commit_changes`: returns working-tree files with queue assignment information.
- `inspect_commit`: returns one queued entry, optionally including the saved patch.
- `update_commit_message`, `update_commit_tests`, `add_commit_files`, `remove_commit_files`: update queued entry metadata and file assignment.
- `remove_commit`, `restore_commit`, `clear_commits`: archive, restore, or clear queued entries.
- `begin_commit_edit`, `save_commit_edit`, `abort_commit_edit`: safely edit an existing queued patch.

Initial `/mcp/browser` tools:

- `browser_navigate`, `browser_inspect`, `browser_click`, `browser_fill`, `browser_press`, `browser_wait_for_selector`, `browser_wait_for_text`, `browser_wait_for_navigation`, and `browser_screenshot`.

`browser_screenshot` returns bounded inline PNG image data for immediate agent inspection and stores the screenshot as a Server-managed audit artifact. Agents should use the returned artifact id with `/mcp/audit` when they need to reload the screenshot later; they should not request direct access to `/tmp` screenshot paths.

Initial `/mcp/audit` tools:

- `audit_query`: filters durable audit events by workspace, working-directory id, repository path, branch, commit, event kind, source, outcome, and time range.
- `audit_timeline`: returns compact recent history for agent context.
- `audit_get_event`: returns full details for one audit event.
- `audit_load_artifact`: loads a Server-managed artifact by opaque artifact id and can return inline image data when requested.

If `start_workspace` cannot find `agent-up.json`, it instructs the agent to read `docs/user-docs/agent-up-json.md`, search for an existing `agent-up.json`, or ask the user before creating one.

`commits next` remains CLI-only because staging and popping a queued entry is developer-owned review work. Agents stop after `get_commits_status`.

MCP enqueue operations require conventional commit messages scoped to the queued slice, such as `fix(Commits): validate queue metadata`. When the Server recognizes feature-sliced paths under `Features/<Slice>/`, enqueue operations reject entries that span multiple slices or whose slice label does not match the recognized slice. Repositories without recognized vertical-slice paths still require a slice label and matching commit-message scope.

Agents should follow the default prefix policy from `get_agent_up_context`: `feat` for user-facing additions, `fix` for user-facing fixes, `test` for test-only or smoke-validation changes, `refactor` for no-behavior source changes, `chore` for maintenance, packaging, CI, or tooling with no customer runtime effect, `style` for CSS/HTML-only changes, and `docs` for documentation-only changes. Repositories may refine those boundaries with `prompts.commitPolicy` in `agent-up.json`.

Cross-slice guidance or documentation updates should be queued in a separate guidance/docs entry instead of being bundled into an implementation slice.

Future tools will add richer diagnostics and Playwright export without moving orchestration out of the Server.

## Commit Queue Reminders

MCP has no server-side lifecycle callback for when an agent finishes a turn. The Server cannot push a post-job reminder; it can only respond to tool calls.

Agents should call `guard_commits` before starting a new coding task. If it fails, they should stop instead of making new changes unless the user explicitly asked to inspect, debug, or continue the existing queued or working-tree changes.

Claude Code installations can surface commit queue reminders with a client-side `Stop` hook. Configure the hook to run `agentup commits guard` and print a reminder when tracked files are dirty but not assigned to a queued entry:

```json
"hooks": {
  "Stop": [
    {
      "matcher": "",
      "hooks": [
        {
          "type": "command",
          "command": "agentup commits guard 2>/dev/null | grep -q 'modified file(s) are not assigned' && echo '[agent-up] Unqueued changes detected - run: agentup commits enqueue' || true"
        }
      ]
    }
  ]
}
```

`enqueue_commit` intentionally restores tracked files after saving the patch. Its success message must tell agents not to re-apply or modify those files because the queue owns them until the developer runs `agentup commits next`.

Mutating commit queue operations are blocked while Git reports an active merge, rebase, cherry-pick, revert, or bisect. Integrations should surface the returned operation state and ask the developer to finish or abort the Git operation first.

MCP commit tools enforce Agent-Up conventional commit prefixes. Use `feat` only for user-facing additions, `fix` only for user-facing fixes, `chore` for internal changes with no user effect and mostly non-runtime files, `refactor` for internal file changes with no user effect unless the change affects a public package, `style` for CSS/HTML-only changes, and `docs` for documentation-only changes including README and similar docs.

## Automation Flow

AI agents interact with applications through MCP:

```text
inspect_page
↓
click
↓
fill
↓
press
↓
wait
↓
screenshot
```

The Server executes browser operations. Browser actions and screenshots are recorded into durable audit history with workspace, workdir, branch, commit, and outcome context.
