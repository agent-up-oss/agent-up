---
title: Audit
---

# Audit

<DocWhat>
HTTP routes owned by the Audit slice.
</DocWhat>

<DocContract label="Prefix">/api/audit</DocContract>

| Method | Path | Operation |
|---|---|---|
| `POST` | `/api/audit/record` | Record |
| `GET` | `/api/audit/workspaces/{workspaceId}/applications/{application}` | QueryApplication |
| `GET` | `/api/audit/workspaces/{workspaceId}/applications/{application}/stream` | Stream |
