---
title: Validation
---

# Validation

<DocWhat>
HTTP routes owned by the Validation slice.
</DocWhat>

<DocContract label="Prefix">/api/workspaces</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/workspaces/{workspaceId}/validation-flows` | List |
| `PUT` | `/api/workspaces/{workspaceId}/validation-flows` | Save |
| `DELETE` | `/api/workspaces/{workspaceId}/validation-flows/{id}` | Delete |
| `GET` | `/api/workspaces/{workspaceId}/validation-flows/{id}` | Get |
| `GET` | `/api/workspaces/{workspaceId}/validation-flows/{id}/playwright` | Export |
| `POST` | `/api/workspaces/{workspaceId}/validation-flows/{id}/run` | Run |
