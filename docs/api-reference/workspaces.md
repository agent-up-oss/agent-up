---
title: Workspaces
---

# Workspaces

<DocWhat>
HTTP routes owned by the Workspaces slice.
</DocWhat>

<DocContract label="Prefix">/api/workspaces</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/workspaces` | GetAll |
| `POST` | `/api/workspaces` | Register |
| `DELETE` | `/api/workspaces/{id}` | Delete |
| `GET` | `/api/workspaces/{id}` | GetById |
| `GET` | `/api/workspaces/{id}/overview` | GetOverview |
| `POST` | `/api/workspaces/{id}/start` | Start |
| `PATCH` | `/api/workspaces/{id}/state` | UpdateState |
| `POST` | `/api/workspaces/{id}/stop` | Stop |
| `GET` | `/api/workspaces/events` | SubscribeEvents |
| `POST` | `/api/workspaces/tutorial/cleanup` | CleanupTutorialWorkspaces |
