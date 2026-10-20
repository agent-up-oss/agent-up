---
title: Forge
---

# Forge

<DocWhat>
HTTP routes owned by the Forge slice.
</DocWhat>

<DocContract label="Prefix">/api/forge</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/forge/callback` | Callback |
| `GET` | `/api/workspaces/{workspaceId}/forge` | Get |
| `POST` | `/api/workspaces/{workspaceId}/forge/comments` | Comment |
| `POST` | `/api/workspaces/{workspaceId}/forge/login` | Login |
| `POST` | `/api/workspaces/{workspaceId}/forge/logout` | Logout |
| `POST` | `/api/workspaces/{workspaceId}/forge/proposals` | OpenProposal |
| `POST` | `/api/workspaces/{workspaceId}/forge/refs` | UpdateRef |
| `POST` | `/api/workspaces/{workspaceId}/forge/reviews` | SubmitReview |
