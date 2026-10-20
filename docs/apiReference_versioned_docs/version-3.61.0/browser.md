---
title: Browser
---

# Browser

<DocWhat>
HTTP routes owned by the Browser slice.
</DocWhat>

<DocContract label="Prefix">/api/browser</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/browser/chromium-status` | ChromiumStatus |
| `GET` | `/api/browser/current-url/{workspaceId}` | GetCurrentUrl |
| `GET` | `/api/browser/events` | StreamAsync |
| `GET` | `/api/browser/input/control-mode/{workspaceId}` | GetControlMode |
| `POST` | `/api/browser/input/control-mode/{workspaceId}` | SetControlModeAsync |
| `POST` | `/api/browser/input/viewport/{workspaceId}` | SetViewportAsync |
| `GET` | `/api/browser/mode` | Mode |
| `POST` | `/api/browser/navigate-back/{workspaceId}` | NavigateBack |
| `POST` | `/api/browser/navigate-forward/{workspaceId}` | NavigateForward |
| `POST` | `/api/browser/navigate/{workspaceId}` | Navigate |
| `GET` | `/api/browser/rdp-viewer` | Viewer |
| `GET` | `/api/browser/rdp/{workspaceId}` | StreamAsync |
| `GET` | `/api/browser/rdp/{workspaceId}/frame` | LatestFrame |
| `POST` | `/api/browser/reload/{workspaceId}` | Reload |
| `GET` | `/api/browser/remote-session/{workspaceId}` | Get |
