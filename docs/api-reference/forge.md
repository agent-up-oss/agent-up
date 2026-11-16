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
| `GET` | `/api/forge/github/install/callback` | GithubInstallCallback |
| `GET` | `/api/forge/github/manifest/callback` | GithubManifestCallback |
| `GET` | `/api/forge/github/register` | GithubRegister |
| `GET` | `/api/workspaces/{workspaceId}/forge` | Get |
| `POST` | `/api/workspaces/{workspaceId}/forge/comments` | Comment |
| `GET` | `/api/workspaces/{workspaceId}/forge/issues` | Issues |
| `POST` | `/api/workspaces/{workspaceId}/forge/login` | Login |
| `POST` | `/api/workspaces/{workspaceId}/forge/logout` | Logout |
| `POST` | `/api/workspaces/{workspaceId}/forge/proposals` | OpenProposal |
| `POST` | `/api/workspaces/{workspaceId}/forge/refs` | UpdateRef |
| `POST` | `/api/workspaces/{workspaceId}/forge/reviews` | SubmitReview |
