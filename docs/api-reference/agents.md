---
title: Agents
---

# Agents

<DocWhat>
HTTP routes owned by the Agents slice.
</DocWhat>

<DocContract label="Prefix">/api/autonomy</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/autonomy` | Get |
| `PUT` | `/api/autonomy` | Put |
| `DELETE` | `/api/workspaces/{workspaceId}/agent` | Stop |
| `GET` | `/api/workspaces/{workspaceId}/agent` | Get |
| `POST` | `/api/workspaces/{workspaceId}/agent` | Schedule |
| `POST` | `/api/workspaces/{workspaceId}/agent/authenticate` | Authenticate |
| `PUT` | `/api/workspaces/{workspaceId}/agent/autonomy` | PutAutonomy |
| `DELETE` | `/api/workspaces/{workspaceId}/agent/binding` | Unbind |
| `PUT` | `/api/workspaces/{workspaceId}/agent/binding` | Bind |
| `POST` | `/api/workspaces/{workspaceId}/agent/cancel` | Cancel |
| `GET` | `/api/workspaces/{workspaceId}/agent/events` | Events |
| `PUT` | `/api/workspaces/{workspaceId}/agent/host-connection` | PutHostConnection |
| `POST` | `/api/workspaces/{workspaceId}/agent/login/callback` | SubmitLoginCallback |
| `POST` | `/api/workspaces/{workspaceId}/agent/login/code` | SubmitLoginCode |
| `GET` | `/api/workspaces/{workspaceId}/agent/loop` | GetLoop |
| `POST` | `/api/workspaces/{workspaceId}/agent/messages` | Prompt |
| `POST` | `/api/workspaces/{workspaceId}/agent/permissions` | Decide |
| `GET` | `/api/workspaces/{workspaceId}/agent/policy` | GetPolicy |
| `PUT` | `/api/workspaces/{workspaceId}/agent/policy` | PutPolicy |
| `PUT` | `/api/workspaces/{workspaceId}/agent/queue-order` | PutQueueOrder |
| `DELETE` | `/api/workspaces/{workspaceId}/agent/role` | DeleteRole |
| `GET` | `/api/workspaces/{workspaceId}/agent/role` | GetRole |
| `PUT` | `/api/workspaces/{workspaceId}/agent/role` | PutRole |
| `POST` | `/api/workspaces/{workspaceId}/agent/sessions/{sessionId}/resume` | Resume |
