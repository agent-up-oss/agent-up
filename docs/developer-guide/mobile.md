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
own Server runtime state. A URL is saved only after the existing workspaces API
responds successfully. Authentication credentials are not currently stored.

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
Use Expo-compatible native controls for platform interactions such as channel
selection. Root application surfaces remain black through device safe areas so
iOS status-bar and Dynamic Island insets do not expose a different background.

## Project structure

Use Expo Router for route entrypoints under `src/app/`. Put client behavior and
UI under product-meaningful slices in `src/features/`, following the same
feature-oriented convention as the .NET projects. Do not commit generated
`android/` or `ios/` projects; Expo owns those platform details until a native
customization requires an intentional prebuild.

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

The web script passes the Server-allocated `WEB_PORT` to Expo when Mobile is
launched from `agent-up.json`; otherwise it uses Expo's default port 8081.

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

Expo writes the static web output to `AgentUp.Mobile/dist/`. The PWA metadata,
install icons, and stable updater service worker live under `public/`;
`src/app/+html.tsx` links the manifest and registers the service worker only in
production exports. The service worker changes only with the bootstrap/update
protocol, rather than being generated from each application payload. Service
workers require HTTPS in deployment, except for browser-supported localhost
development.

Cloudflare Pages must use `AgentUp.Mobile/` as its root directory, run
`npm run build:cloudflare` as the build command, and publish `dist/`. This is
the sole public mobile npm script that does not enter `shell.nix`, because the
Cloudflare build image supplies Node.js but does not supply Nix. The
export entrypoint reads `CF_PAGES_BRANCH` and `CF_PAGES_COMMIT_SHA`, derives a
numeric ticket channel from branch names such as `235-description`, and embeds
the channel, seven-character commit SHA, and commit timestamp into the Metro bundle.
`main` identifies as the `main` channel. Non-matching branches identify as
development builds and are not presented as installed release channels.

The same entrypoint falls back to GitHub Actions variables and then local Git,
so local, channel-release, and Cloudflare exports use one version-identification
path. Do not append Workbox generation to the Cloudflare command: `public/sw.js`
is the stable updater and Expo copies it into `dist/`.

## Branch release channels

Branches whose names begin with a ticket number and hyphen, such as
`235-avalonia-mobile-client`, publish mobile pre-releases. CI exports the Metro
web build with its channel, seven-character commit SHA, and publication timestamp, then
creates an immutable `rc-<channel>-<sha>` GitHub pre-release containing
`agent-up-mobile-web.zip` and `release.json`. Metadata includes the archive's
SHA-256 digest and required-file list, both of which are validated before a
release is cached. Non-matching branches are not
channels, and `main` remains the stable/default channel.

The installed PWA queries GitHub Releases from its Settings screen. It can
upgrade the active channel or switch to another channel by downloading and
caching the complete archive before atomically changing the active-cache
marker and reloading. A release older than the installed release on the same
channel is not offered, so the UI cannot downgrade a channel.

The web export also writes a bootstrap manifest for the initial installation.
On first activation, the service worker caches that complete exported payload
and records it as the active release. Later deployments to the installation
URL must not change an installed PWA; only an explicit Settings update or
channel switch replaces the active release cache.

After activation, the service worker deletes superseded release caches. Opening
the installed PWA with `?agent-up-recovery=1` clears the active release marker
and caches before loading the stable network shell, providing an escape hatch
when a channel payload is broken.
