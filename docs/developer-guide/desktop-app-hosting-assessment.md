---
title: Desktop Application Hosting Assessment
---

# Desktop Application Hosting Assessment

## Decision

Desktop application hosting is feasible, and more of the previous Server-side display work is reusable than the removal commit alone suggests. The right approach is to retain and harden the transport-neutral parts of `AgentUp.Browser.Streaming`, replace its Chromium-specific producer and input adapters, and refuse to carry forward the Desktop compositor workarounds or the misleading RDP contract.

The recommended design is a new, Server-owned `DesktopApplications` capability. Each configured desktop application runs in an isolated Linux graphical session, exposes a standards-based remote framebuffer through a browser viewer, and has a separate automation control plane. Agent-Up Desktop and Agent-Up Mobile load the same authenticated viewer URL only for desktop-application tabs. Existing HTTP-port tabs continue to use their direct WebViews without any streaming path or behavior change.

The first Linux/X11 implementation now follows this direction: `desktopApplications` creates one Xvfb display per application, reuses the streaming subscriber core with a dedicated remote-display viewer, and exposes generation-safe screenshots and coordinate input. The remaining reliability gates in this document continue to define the bar for expanding platform/runtime support.

## Git-history investigation

The relevant history is not one abandoned RDP implementation. It contains four materially different designs:

| Period | Representative commits | What actually ran |
|---|---|---|
| Direct Desktop browser | `b44740f9`, `b57405b2`, `6579cb49` | A NativeWebView per workspace/application port. This is the current reliable path and must remain untouched for web applications. |
| Server Chromium screencast | `edc8ecc0`, `9408fc80` | Puppeteer took repeated JPEG screenshots, `ScreencastBroadcastService` sent them over a custom WebSocket, a canvas viewer decoded them, and JSON pointer/keyboard events were translated back to Puppeteer input. This established multi-client Server ownership and visible agent clicks. |
| RDP-labelled screencast | `f69f0059`, `21d36c75`, `48c64080` | The same JPEG/WebSocket implementation was renamed to RDP routes and types. `IronRdpBrowserRemoteSessionProvider` only checked that the IronRDP assembly was loaded and returned metadata. No IronRDP server, peer, framebuffer codec, or input channel replaced the screencast. The `21d36c75` diff is principally renames and route changes. |
| Dual mode and recovery state machines | `46687cd6` through `4e843f8a` | Human browsing returned to a direct WebView; AI mode observed the Server stream. Server SSE state, viewer JavaScript state, Desktop snapshot state, polling fallback, subscriber presence, reconnect logic, and X11 paint hints accumulated before the Desktop streaming UI was removed. |

The distinction matters: the project did not prove that RDP itself was flaky, because it did not transport RDP. It proved that repeated full-page JPEG capture plus a custom canvas viewer embedded in NativeWebView, coordinated by several independently timed state machines, was flaky in the supported Desktop environment.

### What the fixes tell us

The commit sequence records concrete failure classes that a new design must test:

- `14a0350c`, `5a23cd24`, `28221971`, and `dad36195` repeatedly recovered viewer navigation after error pages, tab switches, and Server/application restarts.
- `9f10c714`, `2445a269`, `19d75530`, and `0fac3802` changed first-frame, cached-frame, polling, bootstrap, and stale-image behavior. A circular dependency existed at one point: the UI waited for streaming state before exposing the viewer, while frame production waited for a subscriber.
- `0da4fda5`, `caee07f0`, `781ba2e8`, `38c77b8c`, `d8b9d2d2`, and `184bce59` addressed freezes, NativeWebView repainting, JavaScript/Avalonia reconnect coordination, and an X11 compositor workaround. These are display-consumer failures, not application health failures.
- `46687cd6` removed viewer input and `23b54b55` restored direct WebViews for human use. This shows that control-authority switching and the streaming failure domain became entangled.
- `e360414d` introduced per-subscriber foreground/background throttling, while `5406baca` and `6926276d` adjusted destruction and navigation ownership. These are useful lifecycle cases but should not require client visibility to determine Server session health.
- `6bda7b8d` explicitly described the connection logic as untestable and introduced a pure Server state derivation plus tests. The later patches demonstrate that a testable reducer helped but could not validate native WebView composition or eliminate competing state machines.

The final removal in `4e843f8a` removed approximately 1,800 lines of Desktop viewer/recovery code and its Desktop tests. It did **not** remove the Server endpoints or the extracted class library. `AgentUp.Browser.Streaming` is still in `agent-up.sln`, is still referenced by `AgentUp.Server`, and its controllers and services are still registered. Calling it orphaned was therefore inaccurate: it is a live but user-inaccessible Server subsystem with residual tests and no dedicated test project.

### Current implementation inventory

The extraction in `99d0a8bd` moved code, but did not make all of it generic. The project currently contains three different categories:

1. **Reusable core candidates.** `BrowserRemoteDisplayService`, `WorkspaceSubscriberSet`, and the receive/send portions of `BrowserEventBus` already implement multiple subscribers, a latest-frame cache, presence, input-activity hints, typed text versus binary frames, disconnect, and bounded event subscribers. `IStreamSessionEventSink` is also a useful inversion point. These should be moved or renamed behind application/session-neutral interfaces rather than rewritten merely because their names say Browser.
2. **Reusable patterns, not reusable types.** `StreamStateDerivation`, `WorkspaceStreamInputs`, `BrowserSessionState`, remote-session DTOs, the viewer's JavaScript state machine, and the associated tests encode valuable lifecycle and reconnect cases. Their actual state names assume Chromium, URL reachability, workspace-wide identity, and a single page, so desktop hosting needs a per-application state model and session generation rather than importing these types.
3. **Browser-only adapters.** `HeadlessBrowserSessionManager`, `HeadlessBrowserSessionAccessor`, `CdpBrowserExecutor`, `HeadlessBrowserCommandDispatcher`, `BrowserScriptProvider`, `BrowserInputDispatcher`, and `BrowserSessionStore` directly depend on Puppeteer, CDP, DOM scripts, browser pages, or browser profiles. They should remain in the Browser capability. A desktop framebuffer producer, graphical-session supervisor, accessibility provider, and desktop input provider replace them.

The current viewer is reusable as a protocol prototype and a regression corpus, not yet as a production viewer. It already handles binary frames, coordinate scaling, pointer buttons, wheel input, key up/down, text paste, presence, reconnect delays, and observable state snapshots. However, it also retains the exact canvas/WebKitGTK paint path associated with the old failures and an HTTP polling fallback that can mask a dead producer with cached pixels.

### Reuse decision by component

| Existing effort | Reuse level | Desktop-hosting decision |
|---|---|---|
| `BrowserRemoteDisplayService` | **Refactor and reuse** | Preserve subscriber fan-out, latest-frame delivery, presence, and disconnect semantics. Key by session ID plus generation, isolate slow subscribers, handle fragmented WebSocket messages, use caller cancellation, enforce maximum message/frame sizes, and remove polling-viewer TTL from authoritative health. |
| `WorkspaceSubscriberSet` | **Refactor and reuse** | Preserve subscriber records and per-viewer presence. Add bounded per-subscriber latest-frame slots instead of awaiting every socket sequentially, plus authenticated viewer identity and metrics. |
| `BrowserEventBus` | **Reuse pattern/core after renaming** | Cached last-state delivery is valuable. Scope cache entries to workspace + application + generation and ensure eviction on every terminal lifecycle path. Do not create a parallel SSE truth if session metadata can carry the same versioned state. |
| `IStreamSessionEventSink` | **Reuse** | Generalize names and make it the narrow notification boundary between the graphical-session producer and Server-owned state coordinator. |
| `StreamStateDerivation` and tests | **Reuse pattern and scenarios** | Keep pure state derivation and precedence tests. Replace Chromium/download/URL inputs with runtime prerequisites, display readiness, app process, window readiness, framebuffer readiness, automation capability, and session generation. |
| `RdpViewerPage` / `rdp-viewer.js` | **Prototype only** | Mine input normalization, reconnect, presence, and snapshot tests. Do not ship it unchanged. First prove its canvas path on real Desktop and Mobile engines, or replace it with the chosen protocol's maintained viewer/client. Remove cached-frame HTTP polling. |
| `BrowserRemoteSessionDto` and provider boundary | **Reuse contract shape** | Keep discoverable transport/viewer/capability metadata, but make it per application and versioned. Replace browser viewport/control fields with logical framebuffer, generation, observer/controller capability, and health. |
| `IronRdpBrowserRemoteSessionProvider` | **Do not reuse as an implementation** | It is metadata plus an assembly-name check, not an RDP transport. Keep IronRDP only if a spike implements and tests a real compatible server/viewer path. Rename existing routes so they no longer claim RDP. |
| `BrowserInputDispatcher` | **Reuse event vocabulary only** | Pointer, wheel, key, and text operations transfer well. Replace Puppeteer calls with a desktop input provider, validate bounds/generation, track pressed keys, and synthesize key-up during lease loss/disconnect. |
| `HeadlessBrowserSessionManager` display loop | **Do not reuse** | Repeated `Page.ScreenshotDataAsync` full JPEG capture is the old producer. A compositor/framebuffer or protocol server supplies incremental display updates. Preserve only supervised/idempotent lifecycle lessons. |
| Desktop `ViewerStateStore`, compositor hints, snapshot poller, and recovery methods removed in `4e843f8a` | **Regression evidence only** | Convert their cases into black-box acceptance tests. Do not restore client-side ownership, WebView destroy/recreate heuristics, focus-based paint detection, or X11 `XClearArea` workarounds. |
| Existing Browser MCP/CDP inspection | **Keep unchanged for web apps** | It cannot inspect native widgets. Share audit/artifact/result conventions through public controller/DTO boundaries, not Browser internals. |

This results in meaningful reuse: the Server fan-out/event primitives, protocol vocabulary, session metadata shape, pure-state approach, and a large regression scenario set survive. The Chromium capture loop, false RDP layer, browser automation adapters, and Desktop recovery machinery do not.

### Required cleanup before extending it

Before desktop hosting is built on the reusable core, make the current repository honest and testable:

- move transport-neutral code into a product-meaningful shared display/session boundary or rename `AgentUp.Browser.Streaming` to reflect its actual ownership;
- move Puppeteer/CDP types back behind the Server Browser slice or into a browser-specific adapter so the shared core has no Puppeteer package dependency;
- either implement real RDP or rename `/api/browser/rdp`, `RdpViewerPage`, and `IronRdpBrowserRemoteSessionProvider` to `screencast`/`remote-display` terminology;
- add a corresponding test project if the streaming core remains a production project, and migrate the currently Browser-hosted tests to it where appropriate;
- disable or remove the inaccessible residual Server routes until a supported client uses them, rather than treating their presence as proof of a supported transport.

Desktop hosting should begin only after that separation, with an explicit session contract and a real display producer plugged into the hardened reusable core.

## Scope and compatibility

The first stable release should support Linux GUI applications that can run in the selected Linux host environment. A compatibility adapter may launch supported Windows binaries through a pinned Wine runtime, but "desktop application" must not imply that arbitrary Windows or macOS software works. macOS applications cannot run through a Linux compatibility layer. Wine support needs its own compatibility matrix, image/runtime version, health checks, and opt-in configuration.

The Server host needs Linux graphical-session prerequisites. Native Windows and macOS Agent-Up Server hosts should initially run the desktop session inside a supported Linux VM or container boundary rather than acquiring separate platform capture and input implementations. A later native-host adapter can satisfy the same session contract without changing clients, validation flows, or MCP tools.

This feature hosts development applications around a workspace; it does not turn Agent-Up into a general application deployment or desktop-as-a-service product.

## Proposed configuration shape

Use a separate root collection instead of adding flags to legacy `applications`. This makes the runtime and security difference visible and prevents existing application launch behavior from changing.

```json
{
  "desktopApplications": [
    {
      "name": "Editor",
      "command": "dotnet run --project src/Editor",
      "path": ".",
      "install": "dotnet restore src/Editor",
      "runtime": "linux",
      "window": {
        "width": 1440,
        "height": 900
      },
      "environment": {},
      "environmentFiles": []
    }
  ]
}
```

The proposed contract has these rules:

- `name`, `command`, `path`, `install`, `environment`, and `environmentFiles` retain the validation, direct process launch, path containment, output streaming, and restart semantics of local applications.
- `runtime` is an allowlisted adapter identifier, initially `linux`; a future `wine` value is accepted only when that adapter is installed and its compatibility contract is met. It is not a free-form executable or image name.
- `window` selects a fixed initial logical framebuffer. Runtime resizing is a later capability because resize races are a common source of mismatched pointer coordinates and flaky validation.
- The Server returns an explicit application kind (`web`, `desktop`, or the existing nonvisual kinds) in workspace application DTOs. Clients must not infer kind from the presence or absence of ports.
- Desktop applications may still declare Agent-Up-owned ports when their process also exposes network services, but the visual tab is the desktop session rather than an HTTP port.
- Configuration registration fails safely when desktop hosting is unsupported. It must not silently launch the command without isolation or silently reinterpret it as a normal local application.

The final property names require a focused schema/design review before implementation. Once accepted, the JSON reference, examples, configuration resources returned through MCP, DTO contract tests, and `AGENTS.md` must change in the same implementation commit.

## Runtime architecture

### Server-owned vertical slice

Add `AgentUp.Server/Features/DesktopApplications/` with `Controllers/`, `DTOs/`, `Models/`, `Providers/`, `Services/`, and justified `Interfaces/`. The slice owns graphical-session lifecycle, remote-display access, control leases, semantic inspection, screenshots, and desktop-specific health. Existing `Applications` and `Processes` controllers remain the boundaries for shared registration/process operations; cross-slice calls exchange IDs and DTOs.

The Server remains the only source of truth. Desktop and Mobile render session state and submit user input; they never start compatibility processes, maintain authoritative reconnect state, or decide that a session is healthy.

### One isolated session per application instance

Use a supervised session with these components:

```text
application process
  -> isolated Xvfb display
  -> X11 framebuffer capture
  -> bounded latest-frame WebSocket fan-out
  -> shared browser viewer

application accessibility bus
  -> AT-SPI provider
  -> Server semantic automation service
```

The initial implementation uses the hardened existing fan-out core with Server-encoded PNG frames and XTest input. Unlike the removed browser path, capture reads the X11 framebuffer directly, slow subscribers cannot block the producer, no HTTP frame-polling fallback exists, and session generation is explicit. A maintained VNC/noVNC or WebRTC provider can replace this behind `IDesktopDisplayProvider` if soak measurements show that PNG bandwidth or frame cadence is insufficient; neither adds signalling or native-client complexity to the initial contract.

Every application instance receives a unique display, runtime directory, accessibility bus, compatibility prefix, and session identifier. Apply CPU, memory, process-count, and storage limits. The supervisor starts dependencies in order, waits for explicit readiness, captures exit reasons, and tears the complete process group down idempotently on stop or restart. A desktop process being alive is not sufficient readiness: display, window discovery, framebuffer updates, semantic provider, and viewer handshake have separate health states.

Do not expose raw VNC/RDP ports. Bind display services to loopback or a private per-session socket and proxy them through authenticated Server routes. The Server API publishes versioned viewer metadata, not implementation-specific process details.

### Session and viewer contract

Identify sessions by workspace ID, application ID, and an opaque generation. The generation changes on every restart so delayed frames and input from an old process cannot affect the new one.

Suggested REST/WebSocket surface:

```text
GET  /api/workspaces/{workspaceId}/desktop-applications/{applicationId}/session
POST /api/workspaces/{workspaceId}/desktop-applications/{applicationId}/viewer-ticket
GET  /desktop-viewer?ticket={single-use-ticket}
WS   /api/desktop-sessions/{sessionId}/display?ticket={single-use-ticket}
```

The metadata includes session generation, lifecycle/health state, logical size, viewer URL, input capability, semantic-automation capability, and a machine-readable reason when degraded. Viewer tickets are short-lived, audience-bound, and single-use so credentials do not live in a durable query string or app configuration. All routes follow normal REST authorization. TLS is required for non-loopback clients.

Multiple Desktop and Mobile viewers may observe one session. Exactly one human client may hold the input lease at a time. MCP automation obtains a short exclusive operation lease, performs one bounded action, and releases it; clients render the current owner and become read-only while another owner acts. The Server serializes input per session. This avoids interleaved touch, mouse, keyboard, and agent events without reviving a distributed "human versus AI mode" state machine.

Flow control is mandatory: keep at most the newest pending framebuffer update per subscriber, discard superseded frames, bound all queues, send protocol heartbeats, and reconnect from a full frame. Viewer disconnects do not restart the application. Application generation changes force a clean viewer reconnect. Client visibility changes may reduce update rate but cannot become authoritative lifecycle input.

## Desktop and Mobile integration

Both clients use the Server-provided viewer page, keeping the protocol implementation in one web asset and the clients thin.

- AgentUp.Desktop creates a dedicated NativeWebView for a selected desktop-application tab and navigates it to the ticketed viewer URL. Existing HTTP tabs retain their current direct-port NativeWebViews.
- AgentUp.Mobile uses a native WebView on Android/iOS and an iframe-compatible viewer surface in the PWA. The current Mobile application screen is only a placeholder, so desktop streaming should be implemented together with the general application-tab content contract rather than as a second temporary path.
- Both clients forward focus, viewport, touch/pointer, and keyboard intent through the viewer protocol. They show Server health and reconnect states but do not infer application readiness from the most recent frame.
- Clipboard and file transfer are disabled in the first release. They require explicit authorization, size limits, audit records, and platform behavior tests before being enabled.
- Viewer accessibility controls and an explicit "take control" action are required. Touch input maps through the fixed logical framebuffer; pinch zoom changes only client presentation and never the Server coordinate space.

No streaming code should be added to the existing web application tab path. A regression test must prove that selecting, hiding, restoring, navigating, and validating an HTTP tab creates no desktop session and uses no viewer route.

## Automation and MCP parity

Streaming is the human-visible data plane, not the automation API. Agent actions must execute against Server-owned semantic and input providers even when no viewer is connected.

### Semantic path

Use the Linux accessibility bus (AT-SPI) as the primary inspection and action provider. Return a bounded tree containing stable-in-generation node IDs, role, accessible name, value/state, bounds, available actions, focus, and parent/child relationships. Prefer accessibility actions such as invoke, set value, focus, select, and scroll over synthesized input.

Node IDs are valid only for one session generation and one observed tree revision. Actions against stale nodes return a structured stale-element error and a fresh inspection hint; they must not guess at a replacement element. Sensitive values are redacted in inspection and audit records.

Wine applications may expose incomplete semantics. The runtime reports semantic support as a capability and never fabricates parity. This limitation is the main reason Wine should follow native Linux support rather than ship in the first stable milestone.

### Screenshot and coordinate fallback

Screenshots come from the compositor/framebuffer capture provider, not by asking a viewing client to capture its WebView. The result includes session generation, logical width/height, scale, capture timestamp, and an opaque audit artifact ID.

When semantic lookup is unavailable, MCP can move/click at logical coordinates and send allowlisted key/chord/text input. Coordinate actions require the generation and dimensions from the inspected screenshot; stale or differently sized input is rejected. This is an explicit fallback, not the default locator strategy. An optional later visual-locator provider may return a confidence score and candidate bounds, but it must never click automatically below a configured threshold.

### MCP tools

Add desktop-specific tools to the owning slice rather than overloading DOM-shaped Browser tools:

- `desktop_inspect`
- `desktop_click`
- `desktop_fill`
- `desktop_press`
- `desktop_wait_for_element`
- `desktop_wait_for_text`
- `desktop_screenshot`

Each tool takes workspace and application identity, returns structured errors, records the same audit context as Browser tools, and reports whether it used a semantic action or coordinate fallback. The tools can be hosted on a dedicated `/mcp/desktop` endpoint, or a later versioned application-automation endpoint can route by explicit application kind. Existing `/mcp/browser` names and behavior remain unchanged.

## Validation parity

Desktop applications should support the same product-level validation workflow as web applications:

- versioned flows stored in `.agent-up/validation-flows.json` and associated with one application;
- user-meaningful steps and visible expectations;
- create, edit, record, replay, and per-step results in both clients;
- staged pointer movement and attention indicators visible to connected viewers;
- Server-owned screenshots, audit events, diagnostics, and artifacts;
- MCP save/replay operations with structured failures;
- deterministic headless CI replay using the same runtime adapter.

The persisted flow now has a desktop coordinate dialect while retaining the existing web behavior. Desktop click and fill steps store logical framebuffer `x` and `y` targets, key steps store the key value, and replay resolves the current session generation before acting. `Running` and framebuffer `Visible` are the initial desktop expectations. AT-SPI role/name/state locators and text/value assertions remain the next semantic extension; raw AT-SPI object paths must never become persisted identity.

"Playwright support" cannot literally mean generating Playwright DOM tests for a native desktop window. Preserve the workflow and result contract, then export web flows to Playwright and desktop flows to an Agent-Up desktop validation runner. If a future external runner is chosen, it must consume the same semantic flow rather than becoming the persisted source of truth. The UI and MCP response should name the runner accurately instead of labelling native replay as Playwright.

## Reliability requirements

The feature is stable only when failure is observable and bounded. Before enabling configuration by default, require all of the following:

1. **Protocol replacement:** no CDP full-page JPEG capture and no HTTP frame-polling fallback; the implemented X11 PNG producer must remain replaceable behind its provider boundary.
2. **Deterministic lifecycle:** dependency readiness, application readiness, restart generation, timeout, crash, and teardown are explicit states with typed reasons.
3. **Long-running soak:** repeated app restart, Server restart, client reconnect, network interruption, tab switching, background/foreground, and two simultaneous clients pass for at least 24 hours without an unrecoverable black/stale frame or unbounded resource growth.
4. **Input correctness:** automated coordinate and semantic tests cover scaling, touch, keyboard layouts, key-up recovery after disconnect, focus changes, modal windows, menus, scrolling, and stale generations.
5. **Cross-client coverage:** native Linux Desktop/WebKitGTK, Android, iOS, and web PWA clients connect to the same implementation. Platform-required suites run on real native engines; mocked WebViews are insufficient.
6. **Isolation and security:** session sockets are unreachable outside the Server proxy; authorization, ticket replay, path traversal, clipboard denial, input lease contention, process isolation, and secret redaction have adversarial tests.
7. **Backpressure and recovery:** slow viewers cannot grow memory or delay healthy viewers; a dropped update recovers with a full frame; application restart cannot display or accept input for the previous generation.
8. **Web regression:** existing direct WebView applications, Browser MCP, browser profiles, validation replay, OAuth/popup behavior, uploads, diagnostics, and port tabs pass unchanged with desktop hosting enabled and disabled.
9. **Operational diagnostics:** session component versions, state transitions, last-frame age, reconnect count, resource use, semantic capability, and sanitized exit reasons are available through Server diagnostics and audit history.
10. **Packaging:** supported Server packages either include pinned graphical/runtime dependencies or fail prerequisite checks with actionable guidance. No first-use download may silently change the runtime version.

Unit tests around a state reducer are useful but not sufficient. The current E2E test launches a real X11 application on the managed Xvfb display, captures its PNG framebuffer, and sends real XTest pointer and keyboard events. The acceptance suite should grow that fixture to expose buttons, text input, menus, a modal, scrolling, animation, accessibility labels, and a deliberate crash, then run it through the Server viewer, Desktop WebView, Mobile WebView, MCP, and validation boundaries.

## Delivery plan

### Phase 0: remove ambiguity

- Split the live `AgentUp.Browser.Streaming` project into a transport-neutral, tested display/session core and Browser-owned Puppeteer/CDP adapters; remove the false RDP naming and inaccessible residual routes.
- Record the original streaming failure modes as executable regression scenarios.
- Prototype both a maintained VNC/noVNC path and a hardened producer plugged into the extracted fan-out core against the deterministic fixture. Measure idle/active CPU, memory, latency, bandwidth, reconnect time, slow-subscriber isolation, and stale-frame incidence rather than choosing only from architectural preference.

Exit only if the prototype demonstrates independent viewer reconnect, Server-side screenshot capture, semantic click, coordinate click, keyboard input, and two concurrent viewers.

### Phase 1: native Linux, feature flagged

- Add configuration parsing and the Server `DesktopApplications` slice.
- Add supervised isolated sessions, authenticated viewer tickets, one fixed viewport, screenshots, input leasing, AT-SPI inspection/actions, diagnostics, and audit events.
- Add Desktop and Mobile application tabs without modifying HTTP tabs.
- Add MCP tools and desktop validation replay behind an explicit experimental capability flag.

### Phase 2: stability gate

- Run the soak, native-client, security, packaging, and web-regression matrices.
- Treat stale frames, black screens, leaked sessions, stuck keys, wrong-generation input, or manual-restart recovery as release blockers rather than banner-worthy degraded behavior.
- Publish the configuration only after all supported packaged environments pass.

### Phase 3: compatibility adapters

- Add pinned Wine support for a documented application subset.
- Evaluate WebRTC only as an interchangeable display provider when measurements show VNC is insufficient.
- Add clipboard, audio, file transfer, dynamic resize, and visual location independently; none should block the stable core.

## Final assessment

The product idea fits Agent-Up's Server-owned architecture and can coexist cleanly with web applications. Both Desktop and Mobile can consume the same browser-based remote-display surface, while MCP and validation use a more reliable Server-side semantic control plane.

The key condition is to interpret "repurpose" at component granularity. Reuse the fan-out and event primitives after hardening, plus the existing workspace/application lifecycle, audit, diagnostics, validation concepts, authentication, protocol vocabulary, and regression scenarios. Do not reuse the full-JPEG capture loop, claim that the custom wire format is RDP, or make client WebView rendering part of the Server state machine. A real framebuffer producer or maintained protocol, strict session generations, bounded queues, explicit control leases, AT-SPI-first automation, and real cross-client soak tests are the minimum credible route to a stable integration.
