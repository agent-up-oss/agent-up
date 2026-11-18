---
title: Agents
---

<DocEyebrow slice="Agents" status="preview" />

# Agents

<DocWhat>
Each workspace has one active authenticated ACP session and a Server-persisted history of that workspace's sessions. The Server schedules the configured ACP executable in the worktree. Clients never launch the CLI or own the session.

Starting or resuming a session replaces the active process without mixing histories between workspaces. Prompts are serialized per workspace.
</DocWhat>

<DocMeta
  owner="AgentUp.Server Agents surface"
  tests="Server Agents HTTP smoke, loop soak, capability Provider smoke"
  rest={'/api/workspaces/{workspaceId}/agent · /api/autonomy'}
/>

<DocSpine>
<DocBeat>Enable the matching ACP package on the Server</DocBeat>
<DocBeat>Complete subscription sign-in when ACP requires it</DocBeat>
<DocBeat>Stream prompts and permission decisions over REST/SSE</DocBeat>
</DocSpine>

<DocContract label="Route">POST .../authenticate</DocContract>

<DocSurfaces>
<DocSurface desktop>The Agent tab lists the workspace's saved sessions across Codex, Cursor, and Claude with agent, generated description, and last branch. The live session is labeled Current; every other row is Resume. Empty history says there are no saved sessions yet. Paste-code sign-in collects the authorization code in the client and posts it to the Server. User prompts are right-aligned catalog bubbles. The live agent run stays fully open; the next question collapses tools, searches, and thoughts to a Worked disclosure. The workspace Settings tab renders the agent picker, saved sessions, and the `GET /api/autonomy` checklist plus Enable autonomy, Activate, Cancel, and the typed loop terminal; the Agent tab shows `SettingsSummary` and a Settings button. ACP `Authentication required` is Waiting for sign-in, not a failed-session banner.</DocSurface>
<DocSurface mobile>The Agents overview tab shows only the workspace's created bound agent. When there is none, its + action opens an inner creation page listing agent modules whose capabilities are enabled; each concise row is Ready or Disabled. Starting from that page lets the Server establish the bind, and a stale bind without a live agent is cleared before choosing again. The workspace Settings tab renders the autonomy checklist, Enable autonomy, Activate, Cancel, and typed terminal; Agents shows `loopSettingsSummary` and opens Settings. Chat is one scroll, so the heading and transcript move together and the message box follows the transcript. The tab bar stays outside that scroll. `session/request_permission` is a blocking decision card. The client shows the sign-in URL and Codex device code from the Server and must not launch `xdg-open` itself. ACP `Authentication required` is Waiting for sign-in, not a raw JSON-RPC error.</DocSurface>
</DocSurfaces>

## Session runtime

The Server stores ACP session IDs and their display metadata under its data directory, partitioned by workspace ID. A generated ACP session title becomes the short description; the current workspace branch is recorded whenever a session is created, resumed, or retitled. Resuming uses ACP `session/load` in the workspace worktree, and deleting a workspace removes its saved session index.

Message submission returns `202 Accepted` after the prompt has been handed to the workspace session; the completed turn arrives over SSE. A second prompt is rejected while a turn is running rather than being executed concurrently. Agent failures become structured operation errors or state events, and stopping an unresponsive adapter escalates from standard-input closure to process-tree termination after a bounded grace period.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a monotonic ID and the `after` query parameter replays retained events after a disconnect. ACP `session/update` notifications are forwarded without discarding their typed payload. `session/request_permission` requests are suspended until a client posts the selected ACP option to `.../permissions`. Every request then closes with a permission-decision event carrying its `requestId`, whoever answered it, so a client replaying the stream drops the prompt. Unsupported ACP client-side requests fail explicitly rather than silently granting access. Slow SSE consumers are disconnected instead of silently losing events.

## Built-in agent capabilities

Codex, Cursor, Claude, and OpenCode are `IAgentCapability` implementations compiled into the Server. `FirstPartyCapabilityCatalog` lists every built-in capability; enabling one only decides whether the Server offers it. The agent picker lists every built-in agent, enabled or not.

`GET /api/workspaces/{id}/agent` descriptors carry `unavailability`, `detail`, and `remedy` when `available` is false, so clients can present availability without attempting a launch. The codes are checked in this order:

- `softwareNotFound` — the capability's `Software` is missing on the Server host. Enabling is refused until it is found.
- `capabilityNotEnabled` — found but not enabled. Clients offer Enable on the row.
- `settingsRequired` — enabled, but a required setting has no value.
- `executableNotFound` — the launch command is not on the search path.

`Agents:<id>:Command` replaces only the executable; the capability still supplies its arguments when the override names none, and its launch environment always. Each launch calls `IAgentCapability.Launch(settings)` with the values stored for that capability, and wraps it in one pinned Nix shell holding the agent's packages and every enabled runtime's. `AgentLaunchResult.Environment` reaches the ACP process; a secret travels there, never in arguments. Optional auth/login stays on the module.

A Development or Testing Server also lists `test-agent-scripted` so the Chat tab can start a session without a vendor CLI. `nix-shell shell.nix` seeds a local enabled set under `.agent-up-dev` and installs the Codex, Claude, and Cursor ACP adapters there. The Server adds `.agent-up-dev/bin` and its npm bin to the search path it probes and launches with (`AGENT_UP_DEV_ROOT`, otherwise the nearest `.agent-up-dev`). Runtime capability E2E sets `AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP=1` so that hook does not download those adapters; Desktop enables only `docker` and `dotnet`.

### OpenCode

OpenCode launches as `opencode acp`, delivered from the pinned nixpkgs by its own Nix package, so Nix is its only required software. It has no sign-in: its settings are `endpoint` (url, required), `token` (secret, optional), and `model` (text, required). `OpencodeConfigProvider` writes `OPENCODE_CONFIG_CONTENT` with an `@ai-sdk/openai-compatible` provider at the endpoint's `/v1` base URL, and names the token as `{env:AGENT_UP_OPENCODE_API_KEY}` so the token stays out of the configuration document. The configuration sets edit, bash, and webfetch to `ask`, so OpenCode emits `session/request_permission`, `SupportsPermissionRequests` is true, and a permission policy can govern it in both author and reviewer roles.

When ACP reports that authentication is required, the session stays scheduled and `POST .../authenticate` starts that agent's subscription login CLI instead of asking the ACP process to open a local browser. The Server streams the printed sign-in URL and any device code to Desktop and Mobile, waits until the CLI exits, then restarts ACP against the Server data directory's `agent-cli-home`. The ACP JSON-RPC `Authentication required` / `-32000` notice is a sign-in prompt, not a failed session. A pasted-code sign-in that finishes without a captured subscription token returns to authentication instead of starting unsigned. Agent-Up does not collect API keys.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP client cannot safely proxy an interactive terminal.

## Workspace bind and unattended loop

A workspace may record one bound agent module id. Unbound is the interactive picker. Autonomy stays off until the Server is enabled (persisted, env var still wins) and the workspace is activated against the ordered `GET /api/autonomy` chain, a policy, a host connection, and assigned issues. Roles prefix the prompt and may filter advertised MCP tools. The full contract is on Autonomy.

## Product decisions

An agent writes down a product decision with `train_record_decision` on `/mcp/training` and carries on: recording never blocks and returns no verdict. Record a call where real alternatives existed and a reasonable person could have chosen otherwise — scope and priorities, what a screen says and how it behaves, where a boundary in the code goes — and leave out instructions you were given, questions with one sensible answer, and routine work. The headline is the decision in one line a product owner can judge at a glance; the detail is what was decided and the evidence is what it rested on, with up to four audit artifact ids (a `browser_screenshot` capture, for one) that the Server copies into its own evidence store so the reference survives a changing worktree.

Verdicts come back through the same space. `train_list_decisions` and `train_get_decision` carry the product owner's verdict and note as soon as they give it, so read the decisions recorded for the area before deciding in it again: `good` is how the owner wants this product built, `bad` is a call not to repeat. The Server serves the same backlog to Desktop's command center and Mobile's **Train** tab over `api/training/*`, and exports the judged decisions as JSONL for post-training.

## Loop coverage

CI runs the Server loop kernel against a locally-inferring ACP test agent on CPU llama.cpp. Hard invariants fail the run. Pooled task-completion outcomes are compared to a versioned baseline. Scenario files under `loop-coverage/` do not name an agent, so the same prompts can drive CPU inference and `test-agent-scripted`. Concurrency and restart for at least three autonomous workspaces stay in the normal E2E tier. A separate soak check drains hundreds of scripted tasks.

The same pinned model also runs behind `test-model-server`, an Ollama/OpenAI-compatible `/v1` endpoint (`GET /v1/models`, `POST /v1/chat/completions`, plain or streamed). It renders requests into Qwen's own tool-call format and compacts the coding agent's long system prompt and tool schemas so a CPU turn stays in seconds. The `opencode-inference` check points the real OpenCode binary at it through the built-in capability and requires one unattended author iteration to write the requested file under a permission policy. It also drives a published Server process over REST and the event stream end to end, from enabling the capability to answering Allow once. `--api-key` makes the model server refuse requests without that bearer token, which is how the check proves the token saved in Settings reaches the backend. It is also the quickest way to try OpenCode locally: start `test-model-server --port 18434` and set the OpenCode endpoint to `http://localhost:18434`.

<DocNext href="/developer-guide/agents/autonomy" title="Autonomy">
Bind, policy, unattended loop, roles, scripted ACP, and soak.
</DocNext>
