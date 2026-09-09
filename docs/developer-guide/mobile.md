---
title: Mobile development
---

# Mobile development

`AgentUp.Mobile/` is a single Expo and React Native TypeScript client for
Android, iOS, and the installable web PWA. It lives at the repository root but
is not part of `agent-up.sln`.

The client follows the same ownership model as Desktop: it displays
Server-owned state and submits requests to the Server. Runtime state and
orchestration must remain in `AgentUp.Server`.

The Servers client slice stores configured HTTP or HTTPS Server base URLs and
the active selection in PWA local storage. Only one Server is active at a time;
selecting another sidebar icon changes the client target and does not copy or
own Server runtime state. A URL is saved only after the Server authentication
status probe succeeds. If login is required, the client requests the single
administrator password and stores the resulting access token with the Server
selection; if authentication is disabled, it skips that login step. Remote
servers must use HTTPS; loopback HTTP URLs remain supported for local
development.

As an explicit exception to the general application-package isolation rule,
Mobile consumes `@agent-up/audit` from the local `AgentUp.WebAudit/` package
until registry publication is enabled. Agent-Up-managed web launches expose
the injected workspace and application identity to Expo. Server connection
attempts record best-effort success or failure events at the orchestrating
Server's injected audit endpoint. Outside a managed launch, audit delivery
falls back to the Server URL being tested;
audit delivery must never replace the connection result shown to the user.

Mobile surfaces follow the docs site's black, green, off-white, and muted
gray-green visual system, including its compact 8px card and control radii.
Root application surfaces remain black through device safe areas so
iOS status-bar and Dynamic Island insets do not expose a different background.

## Project structure

Use Expo Router for route entrypoints under `src/app/`. Put client behavior and
UI under product-meaningful slices in `src/features/`, following the same
feature-oriented convention as the .NET projects. Do not commit generated
`android/` or `ios/` projects; Expo owns those platform details until a native
customization requires an intentional prebuild.

## Navigation

The mobile client is a gated stack, not a bottom-tab shell.

- `/connect` is the entry screen until a Server URL is saved successfully.
- After connect, `/(main)` renders a persistent top nav bar and a collapsible
  sidebar. Screen content renders below the nav bar. Each screen sets the nav
  title, optional right action, and optional custom sidebar content through
  `useShellConfig`.
- The default sidebar lists workspaces for the active Server and lets the user
  switch workspaces. The first workspace is selected automatically when the list
  loads.
- Workspace routes live under `/(main)/workspace/[workspaceId]/`. The dashboard
  is the workspace home page. Agent chat and application spaces are deeper stack
  routes.
- Only the workspace agent screen uses a bottom bar. It switches between the
  placeholder chat view and the existing Git changes panel.

## Workspaces and Git slices

`src/features/workspaces/` owns workspace selection, refresh, clone, and the
workspace dashboard. Selection lives in `WorkspacesProvider`, which is mounted in
the root layout so every authenticated screen reads the same selection.

`src/features/git/` owns the Git changes panel used by the agent Changes tab. It
renders the Server's change tree as indented rows with per-file checkboxes,
opens a file's diff in a modal, and commits the selected paths with the entered
message. Tree flattening and directory/file selection are pure functions in
`providers/GitChangeTreeProvider.ts` so they are covered by node tests without a
renderer.

Both slices reach the Server through
`src/features/servers/providers/ServerRequestProvider.ts`. The servers slice
owns connectivity to a configured Server, so feature slices do not reimplement
timeout, problem-detail, and unreachable-server handling.

A request takes a `ServerSession` — the Server's URL together with the access
token stored for it — rather than a bare URL. The Server requires a bearer token
unless it was started with `AGENTUP_AUTH_DISABLED=true`, so the transport adds
the `Authorization` header whenever the session carries a token.

Work that outlives its own request must check that its session is still the
active one before it writes shared state. `WorkspaceRefresher.isActive` compares
the whole session, URL and token alike, so a clone still running when the
credential changes does not refresh with the credential the Server has since
stopped accepting. The Git screen guards its change-tree and file-diff loads the
same way through `createRequestGate`, keeping each independent so opening a file
does not discard the tree that is still loading.

## Local development

Install dependencies with the repository Nix shell so the expected Node.js
runtime is used:

```bash
cd AgentUp.Mobile
nix-shell ../shell.nix --run 'npm ci'
```

Every public npm script except `build:cloudflare` enters the repository `shell.nix` automatically. The
shell is a development requirement and supplies Node.js and the native
Linux libraries required by Expo's downloaded React Native DevTools binary on
NixOS. It also fetches the DotSlash-managed binary when needed and patches its
Electron executables to use the Nix dynamic linker.

Start the web client, which is the default local development path on every
supported desktop operating system:

```bash
npm run web
```

The start, Android, iOS, and web scripts use Expo's LAN mode. Metro listens on
all network interfaces and advertises the machine's LAN address so physical
devices can connect.

`agent-up.json` builds the production PWA during the application `install` step
and serves the exported `dist/` output through `npm run serve:web`. The export
removes any existing `dist/` directory before writing a fresh static payload so
removed routes and stale hashed assets are not carried forward. The serve script
passes the Server-allocated `WEB_PORT` when Mobile is launched from Agent-Up;
otherwise it uses Expo's default port 8081. Use `npm run web` when you need the
Metro development server with hot reload.
Before Expo starts, the script waits briefly for a previous listener on that
same application port to exit. Each application receives its own allocated port
even when multiple apps declare the same port variable name.

The same development server can open the app through Expo Go on a physical
Android or iOS device:

```bash
npm start
```

Scan the displayed QR code with Expo Go. Native simulator commands remain
available when the required platform tooling is installed:

```bash
npm run android
npm run ios
```

The iOS simulator still requires macOS. Neither the simulator nor a local
Android SDK is required for normal web/PWA development.

## Verification and web export

Run TypeScript checking and create the production PWA bundle before submitting
mobile client changes:

```bash
npm run typecheck
npm run build:web
```

Expo writes the static web output to `AgentUp.Mobile/dist/`. The PWA metadata and
install icons live under `public/`; `src/app/+html.tsx` links the manifest in
production exports. The mobile client does not register a custom service worker.
Installed and Agent-Up-served builds load the current static export from the
network on each visit.

Cloudflare Pages must use `AgentUp.Mobile/` as its root directory, run
`npm run build:cloudflare` as the build command, and publish `dist/`. This is
the sole public mobile npm script that does not enter `shell.nix`, because the
Cloudflare build image supplies Node.js but does not supply Nix. The export
entrypoint passes Agent-Up audit environment variables into the Metro bundle
when present.

`npm run serve:web` serves `dist/` for Agent-Up workspaces with
`Cache-Control: no-store` so local rebuilds are visible without clearing site
data.
