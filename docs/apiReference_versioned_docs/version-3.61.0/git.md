---
title: Git
---

# Git

<DocWhat>
HTTP routes owned by the Git slice.
</DocWhat>

<DocContract label="Prefix">/api/workspaces</DocContract>

| Method | Path | Operation |
|---|---|---|
| `POST` | `/api/workspaces/{workspaceId}/git/branch` | SwitchBranch |
| `GET` | `/api/workspaces/{workspaceId}/git/changes` | GetChanges |
| `POST` | `/api/workspaces/{workspaceId}/git/checkout` | Checkout |
| `POST` | `/api/workspaces/{workspaceId}/git/commit` | Commit |
| `POST` | `/api/workspaces/{workspaceId}/git/discard` | Discard |
| `POST` | `/api/workspaces/{workspaceId}/git/fetch` | Fetch |
| `GET` | `/api/workspaces/{workspaceId}/git/file` | GetFileDiff |
| `GET` | `/api/workspaces/{workspaceId}/git/head` | GetHead |
| `GET` | `/api/workspaces/{workspaceId}/git/log` | GetLog |
| `POST` | `/api/workspaces/{workspaceId}/git/pull` | Pull |
| `POST` | `/api/workspaces/{workspaceId}/git/push` | Push |
