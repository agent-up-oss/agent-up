---
title: Database
---

# Database

<DocWhat>
HTTP routes owned by the Database slice.
</DocWhat>

<DocContract label="Prefix">/api/workspaces</DocContract>

| Method | Path | Operation |
|---|---|---|
| `GET` | `/api/workspaces/{id}/applications/{name}/database/databases` | ListDatabases |
| `POST` | `/api/workspaces/{id}/applications/{name}/database/query` | ExecuteQuery |
| `GET` | `/api/workspaces/{id}/applications/{name}/database/tables` | ListTables |
