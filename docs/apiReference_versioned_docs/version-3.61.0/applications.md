---
title: Applications
---

# Applications

<DocWhat>
HTTP routes owned by the Applications slice.
</DocWhat>

<DocContract label="Prefix">/api/workspaces</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/workspaces/{id}/applications` | GetApplications |
| `GET` | `/api/workspaces/{id}/applications/{name}/metrics` | GetMetrics |
| `GET` | `/api/workspaces/{id}/applications/{name}/output` | GetOutput |
| `POST` | `/api/workspaces/{id}/applications/{name}/restart` | RestartApplication |
| `POST` | `/api/workspaces/{id}/applications/{name}/start` | StartApplication |
| `POST` | `/api/workspaces/{id}/applications/{name}/stop` | StopApplication |
