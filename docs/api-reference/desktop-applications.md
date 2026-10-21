---
title: Desktop Applications
---

# Desktop Applications

<DocWhat>
HTTP routes owned by the Desktop Applications slice.
</DocWhat>

<DocContract label="Prefix">/api/desktop-applications</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/desktop-applications/{workspaceId}/{application}` | Get |
| `POST` | `/api/desktop-applications/{workspaceId}/{application}/key` | Key |
| `POST` | `/api/desktop-applications/{workspaceId}/{application}/pointer-down` | PointerDown |
| `POST` | `/api/desktop-applications/{workspaceId}/{application}/pointer-up` | PointerUp |
| `GET` | `/api/desktop-applications/{workspaceId}/{application}/screenshot` | Screenshot |
| `POST` | `/api/desktop-applications/{workspaceId}/{application}/viewer-ticket` | CreateViewerTicket |
| `GET` | `/api/desktop-applications/session/{sessionId}/display` | Stream |
| `GET` | `/api/desktop-applications/session/{sessionId}/viewer` | Viewer |
