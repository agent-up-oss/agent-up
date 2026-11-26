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
<DocSurface desktop>The Agent tab lists the workspace's saved sessions across Codex, Cursor, and Claude with agent, generated description, and last branch. The live session is labeled Current; every other row is Resume. Empty history says there are no saved sessions yet. Paste-code sign-in collects the authorization code in the client and posts it to the Server. User prompts are right-aligned catalog bubbles. Replies and the agent's prose between steps render markdown through `AgentMarkdownProvider` (headings, lists, quotes, tables, inline code, links, and highlighted fenced code with a Copy action) as `au-chat-reply` prose on the canvas, not a card. Each tool call is one `au-tool-call` row from `AgentToolCallProvider`: a status glyph, the readable name (an `agent-up-*` MCP tool reads as its action with an Agent-Up source), and the argument that matters; it opens to the input and a formatted, bounded output. The live agent run stays fully open; the next question collapses tools, searches, and thoughts to a Worked disclosure. The workspace Settings tab renders the agent picker, saved sessions, and the `GET /api/autonomy` checklist plus Enable autonomy, Activate, Cancel, and the typed loop terminal; the Agent tab shows `SettingsSummary` and a Settings button. ACP `Authentication required` is Waiting for sign-in, not a failed-session banner.</DocSurface>
<DocSurface mobile>The Agents overview tab shows only the workspace's created bound agent. When there is none, its + action opens an inner creation page listing agent modules whose capabilities are enabled; each concise row is Ready or Disabled. Starting from that page lets the Server establish the bind, and a stale bind without a live agent is cleared before choosing again. The workspace Settings tab renders the autonomy checklist, Enable autonomy, Activate, Cancel, and typed terminal; Agents shows `loopSettingsSummary` and opens Settings. Chat renders replies and tool calls from `@agent-up/chat`'s `MarkdownProvider` and `ToolCallProvider`, which follow Desktop's rules case for case. Chat is one scroll, so the heading and transcript move together and the message box follows the transcript. The tab bar stays outside that scroll. A tapped tool call, edit, or thought opens in the catalog `au-sheet` (`TranscriptItemSheet` over `BottomSheet`), titled and laid out by `presentSheet` in `SheetProvider`: half height first, dragged up to full, dragged down, the close button, the scrim, or system back to close. Sections and fold lines still unfold in place. `session/request_permission` is a blocking decision card. The client shows the sign-in URL and Codex device code from the Server and must not launch `xdg-open` itself. ACP `Authentication required` is Waiting for sign-in, not a raw JSON-RPC error.</DocSurface>
</DocSurfaces>

## Session runtime

The Server stores ACP session IDs and their display metadata under its data directory, partitioned by workspace ID. A generated ACP session title becomes the short description; the current workspace branch is recorded whenever a session is created, resumed, or retitled. Resuming uses ACP `session/load` in the workspace worktree, and deleting a workspace removes its saved session index.

Message submission returns `202 Accepted` after the prompt has been handed to the workspace session; the completed turn arrives over SSE. A message sent while a turn is running is never executed concurrently: it is queued as steering, published as a user message marked `steering: true`, and sent as the next turn once the running one ends. Under a loop policy the loop folds it into its own next prompt instead. Agent failures become structured operation errors or state events, and stopping an unresponsive adapter escalates from standard-input closure to process-tree termination after a bounded grace period.

When an agent's `initialize` result advertises `mcpCapabilities.http`, `session/new` and `session/load` offer the Server's own MCP endpoints - orchestration, commits, verification, browser, audit, and training - as HTTP servers on the Server's loopback address. An agent without HTTP MCP is offered none. `AgentMcpServerProvider` owns that list.

Each offered server carries an `X-Agent-Up-Agent-Session` header with a credential `AgentMcpSessionService` issues for that session, replaces when the workspace starts another, and revokes when it stops. `AgentMcpSessionMiddleware` turns a live credential into a caller with the workspace's `workspace` claim, a stable subject, and the team member's operation permissions, so the agent meets the same endpoint floors, permission filter, and workspace pin as a workspace-bound token: it never sees another workspace, and a dev reviewer has no orchestration writes, browser, or commit queue. A credential that is not live is answered `404`. A loopback caller that presents no credential is still the anonymous full-toolset caller human tools rely on, so the header is what an agent is confined by, not the network.

`GET .../events` is an authenticated Server-Sent Events stream. Events have a monotonic ID and the `after` query parameter replays retained events after a disconnect. ACP `session/update` notifications are forwarded without discarding their typed payload. `session/request_permission` requests are suspended until a client posts the selected ACP option to `.../permissions`. Every request then closes with a permission-decision event carrying its `requestId`, whoever answered it, so a client replaying the stream drops the prompt. Unsupported ACP client-side requests fail explicitly rather than silently granting access. Slow SSE consumers are disconnected instead of silently losing events.

## Built-in agent capabilities

Codex, Cursor, Claude, and OpenCode are `IAgentCapability` implementations compiled into the Server. `FirstPartyCapabilityCatalog` lists every built-in capability; enabling one only decides whether the Server offers it. The agent picker lists every built-in agent, enabled or not.

`GET /api/workspaces/{id}/agent` descriptors carry `unavailability`, `detail`, and `remedy` when `available` is false, so clients can present availability without attempting a launch. The codes are checked in this order:

- `softwareNotFound` — the capability's `Software` is missing on the Server host. Enabling is refused until it is found.
- `capabilityNotEnabled` — found but not enabled. Clients offer Enable on the row.
- `settingsRequired` — enabled, but a required setting has no value.
- `executableNotFound` — the launch command is not on the search path.

`Agents:<id>:Command` replaces only the executable; the capability still supplies its arguments when the override names none, and its launch environment always. An override that resolves makes the agent available whatever its capability's state; that is how the test agents take a vendor's slot on a host without the vendor's CLI. The agent inherits none of the Server's environment: `Agents:<id>:Environment:<NAME>` declares a value for it, applied over the capability's launch environment, and the agent's executable, wrapper targets included, must be on the Server's PATH to be mounted in its sandbox. Each launch calls `IAgentCapability.Launch(settings)` with the values stored for that capability, and wraps it in one pinned Nix shell holding the agent's packages and every enabled runtime's. `AgentLaunchResult.Environment` reaches the ACP process; a secret travels there, never in arguments. Optional auth/login stays on the module.

A Development or Testing Server also lists `test-agent-scripted` so the Chat tab can start a session without a vendor CLI. `nix-shell shell.nix` seeds a local enabled set under `.agent-up-dev` and installs the Codex, Claude, and Cursor ACP adapters there. The Server adds `.agent-up-dev/bin` and its npm bin to the search path it probes and launches with (`AGENT_UP_DEV_ROOT`, otherwise the nearest `.agent-up-dev`). Runtime capability E2E sets `AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP=1` so that hook does not download those adapters; Desktop enables only `docker` and `dotnet`.

### OpenCode

OpenCode launches as `opencode acp`, delivered from the pinned nixpkgs by its own Nix package, so Nix is its only required software. It has no sign-in: its settings are `endpoint` (url, required), `token` (secret, optional), and `model` (text, required). `OpencodeConfigProvider` writes `OPENCODE_CONFIG_CONTENT` with an `@ai-sdk/openai-compatible` provider at the endpoint's `/v1` base URL, and names the token as `{env:AGENT_UP_OPENCODE_API_KEY}` so the token stays out of the configuration document. A sandboxed OpenCode process reaches that endpoint through the Server's model proxy, so the stored token never enters the jail. The configuration sets edit, bash, and webfetch to `ask`, so OpenCode emits `session/request_permission`, `SupportsPermissionRequests` is true, and a permission policy can govern it in both author and reviewer roles.

When ACP reports that authentication is required, the session stays scheduled and `POST .../authenticate` starts that agent's subscription login CLI instead of asking the ACP process to open a local browser. The Server streams the printed sign-in URL and any device code to Desktop and Mobile, waits until the CLI exits, then restarts ACP against the same per-workspace CLI home the login used. The ACP JSON-RPC `Authentication required` / `-32000` notice is a sign-in prompt, not a failed session. A pasted-code sign-in that finishes without a captured subscription token returns to authentication instead of starting unsigned. Agent-Up does not collect API keys.

Agent-Up advertises no terminal-auth capability because the authenticated HTTP client cannot safely proxy an interactive terminal.

## Transcript

Desktop and `@agent-up/chat` read the same ACP events with the same rules. Their parity is checked by replaying one recorded session through both, not by eye.

- **Edits.** An edit or a new file opens to a diff in the Git file viewer's line treatment, and its row shows `+N −N`. The diff comes from the richest source the agent sent: the unified diff in OpenCode's `rawOutput.metadata.diff`, then ACP's `diff` content item, then a write's `content`. Long diffs show 40 lines with **Show all**.
- **Names.** A tool keeps the name from its first event, because OpenCode retitles the completed call with the file path. A write reads **Write**, not Edit. When a read's visible content is empty, its `rawOutput` is shown without OpenCode's `<path>`/`<content>` tags. A summary skips a bare `.` directory and names the search pattern.
- **Code.** Fenced code is highlighted with the design system's grammars (`SyntaxHighlightProvider` on Desktop, `@agent-up/design-system/syntax` in Chat). A fence tag resolves by grammar id, a common alias, or an extension. Unknown languages and code over 400 lines stay plain.
- **Tables.** Pipe tables draw with a header band and per-column alignment, and scroll sideways when wide. A reply that is entirely one `markdown` or `md` fence is unwrapped and drawn as markdown.
- **Hierarchy.** A turn alternates between work sections and what the agent said. The agent's text - summaries midway and the closing reply - is always shown and never folds; only the closing reply is headed with the agent's name. The work between two such messages is one section.
- **Sections.** The section the agent is working in is drawn open. Once it is past it - it spoke, or the turn ended - the section folds into one line: **Worked for 48s · 3 edits, 2 reads, 1 thought**. A folded section names failed tools: **· 1 failed**. A reader's own open or close of a section stands.
- **Entries.** Inside a section, only the active entry - a thought still streaming or a call still running - is drawn open. Every other entry is one line: **Thought for 4s**, **Edit server.ts**, **Read README.md**, a command, or an MCP call. A thought opens to its raw text. On Desktop an entry opens in place. On Mobile, a sheet opens over the transcript, so the transcript keeps its scroll position. The sheet shows a thought as prose, an edit as its whole diff, and any other call as labelled arguments and its full output, never raw JSON. Sections and folds open in place on both clients.
- **Folding.** Two or more consecutive finished edits, reads, searches, and thoughts fold into one count line, **2 edits, 3 reads, 1 thought**, that opens to its entries. Other tool calls and the active entry never fold.
- **Timing.** The first section of a turn is timed from the prompt's event timestamp to its last step, later sections from their own first step. The time the agent then spends writing is not counted as work. A tool or thought that took a second or more shows its own duration.
- **Turns.** The Server's prompt result event ends the turn, and every section folds. Steering that runs next therefore gets its own sections and reply instead of joining the previous turn.
- **Streaming.** A streaming reply rebuilds only the blocks whose content changed. Desktop compares block keys and re-publishes a turn's sections only when their structure changes; Chat memoises each block on its key. Both follow new output while the reader is at the end, and stop once they scroll up.
- **Motion.** Nothing in the transcript jumps. Following glides to the end (eased, time-based) and a reader's wheel, press, or upward scroll takes over at once. Sections, folds, tool details, and thoughts open and close by animating their height and opacity over 220 ms (`AgentRevealPresenter` on Desktop, `Reveal` in Chat), and are not measured while closed. A new entry fades in (Chat also lifts it 6 px). Streamed text is revealed at a steady pace that catches up with what arrived in about a quarter of a second (`MotionProvider.RevealStep`, `revealStep`), and a markdown block that did not exist before fades in. Reduced motion turns it all off: the system setting in Chat, `AGENTUP_REDUCED_MOTION=1` on Desktop. Desktop headless tests run with motion off and test the motion math directly.

`AgentUp.Chat/fixtures/opencode-cpu-session.jsonl` is a real seven-turn OpenCode session against the CPU model: write, read, edit, run, search, an Agent-Up MCP call, and a markdown answer. `AgentGoldenTranscriptTests` on Desktop and `GoldenTranscript.test.ts` in Chat replay it and assert what a reader sees. When OpenCode's event shapes change, capture a new session and update both tests together.

## Workspace bind and unattended loop

A workspace may record one bound agent module id. Unbound is the interactive picker. Autonomy stays off until the Server is enabled (persisted, env var still wins) and the workspace is activated against the ordered `GET /api/autonomy` chain, a policy, a host connection, and assigned issues. Roles prefix the prompt and may filter advertised MCP tools. The full contract is on Autonomy.

## Task tools

`/mcp/tasks` is how an agent reads its task and says it is done; the Server makes every Git and host write around it with the member's own credential.

<DocFacts>
<DocFact label="get_task">The task the member is on: issue, stage, proposal and revision, the open revision's reviews, findings at `path:line`, each check's latest result with its log link, comments, and the next step. `workspace.read`.</DocFact>
<DocFact label="submit_work">Authors only. Commits to `agent-up/issue-<n>`, pushes, and opens or updates the proposal with the summary. `git.write`.</DocFact>
<DocFact label="submit_review">Reviewers only. verdict "approve" or "request_changes", a summary, findings at `path`/`line`, and up to six screenshot artifact ids the Server attaches on the host. Stops the reviewer's workspace and ends its task. `workspace.read`; an author is refused by the task service.</DocFact>
</DocFacts>

An agent that ends its turn without these still works: an author's reply becomes its summary, and a reviewer's `VERDICT:` line its verdict.

## Product decisions

An agent writes down a product decision with `train_record_decision` on `/mcp/training` and carries on: recording never blocks and returns no verdict. Record a call where real alternatives existed and a reasonable person could have chosen otherwise — scope and priorities, what a screen says and how it behaves, where a boundary in the code goes — and leave out instructions you were given, questions with one sensible answer, and routine work. The headline is the decision in one line a product owner can judge at a glance; the detail is what was decided and the evidence is what it rested on, with up to four audit artifact ids (a `browser_screenshot` capture, for one) that the Server copies into its own evidence store so the reference survives a changing worktree.

Verdicts come back through the same space. `train_list_decisions` and `train_get_decision` carry the product owner's verdict and note as soon as they give it, so read the decisions recorded for the area before deciding in it again: `good` is how the owner wants this product built, `bad` is a call not to repeat. The Server serves the same backlog to Desktop's command center and Mobile's **Train** tab over `api/training/*`, and exports the judged decisions as JSONL for post-training.

## Team members

Each workspace is one team member: `Author`, `DevReviewer`, or `PmReviewer`. The kind fixes repository read and write, browser control, and who may submit reviews. Authors and dev reviewers run ACP coding CLIs; PM reviewers run the in-process `ollama` agent, which reaches the workspace only through pinned Agent-Up MCP tools. The contract is on Team members.

## Loop coverage

CI runs the Server loop kernel against a locally-inferring ACP test agent on CPU llama.cpp. Hard invariants fail the run. Pooled task-completion outcomes are compared to a versioned baseline. Scenario files under `loop-coverage/` do not name an agent, so the same prompts can drive CPU inference and `test-agent-scripted`. Concurrency and restart for at least three autonomous workspaces stay in the normal E2E tier. A separate soak check drains hundreds of scripted tasks.

The same pinned model also runs behind `test-model-server`, an OpenAI-compatible `/v1` endpoint (`GET /v1/models`, `POST /v1/chat/completions`, plain or streamed) that also answers Ollama's `POST /api/chat`, so the in-process `ollama` agent can run on it with `AGENTUP_OLLAMA_URL` pointed there. An Ollama request offers the model every tool it sends, because that client sends only its own. It renders requests into Qwen's own tool-call format and compacts the coding agent's long system prompt and tool schemas so a CPU turn stays in seconds. MCP tools, whose names carry their server's prefix, are offered to the model only once the conversation names them; with every Agent-Up tool listed, the 1.5B model stopped calling any. The `opencode-inference` check points the real OpenCode binary at it through the built-in capability and requires one unattended author iteration to write the requested file under a permission policy. It also drives a published Server process over REST and the event stream end to end, from enabling the capability to answering Allow once. `--api-key` makes the model server refuse requests without that bearer token, which is how the check proves the token saved in Settings reaches the backend. It is also the quickest way to try OpenCode locally: start `test-model-server --port 18434` and set the OpenCode endpoint to `http://localhost:18434`.

<DocNext href="/developer-guide/agents/autonomy" title="Autonomy">
Bind, policy, unattended loop, roles, scripted ACP, and soak.
</DocNext>
