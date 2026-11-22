---
title: Workspace files
---

# Workspace files

<DocWhat>
This page is the Files slice: the whole workspace folder, read one folder at a time and edited one file at a time. It is a human observability surface beside Git review; agents work in their own checkout and have no Files MCP tools.
</DocWhat>

<DocMeta
  owner="AgentUp.Server Files slice"
  tests="AgentUp.Server.Tests/Features/Files/"
  rest={'/api/workspaces/{workspaceId}/files/*'}
/>

<DocContract label="Route">{'GET /api/workspaces/{workspaceId}/files/directory?path='}</DocContract>

<DocFacts label="Routes">
<DocFact label="GET files/directory">One folder, one level deep: folders first, each entry with `kind`, `size`, its Git `status`, and `changedFiles` for a folder. `truncated` is true past 2000 entries</DocFact>
<DocFact label="GET files/file">One whole file with `version`, its Git `status`, and `content`. `content` is null when `isBinary` or `isTooLarge` (over 1 MB)</DocFact>
<DocFact label="PUT files/file">Replaces an existing text file. The body names `path`, `content`, and the `version` the edit started from</DocFact>
</DocFacts>

## Contracts

- **Lazy by design.** Clients ask for the top level when the tab first opens and for a folder when it is first unfolded. Reload reads the top level and the folders still unfolded. Nothing walks the tree ahead of the reader.
- **Paths are data.** A path must be relative, use `/`, never contain `..`, and never name `.git`. It must still resolve inside the workspace folder once every link on the way is followed. A link that leads outside is left out of listings and refused on read. A bad path is `400`.
- **Git markers come from the Git slice.** `GitQueryController.GetWorktreeChangesAsync` returns changes relative to the workspace folder, so a workspace that is a subfolder of its repository maps statuses correctly. A folder that is not a repository has no markers. `changedFiles` counts changes under a folder that still exist on disk; deleted files are never listed.
- **Optimistic concurrency.** `version` is the SHA-256 of the file's bytes. A save whose `version` no longer matches is `409` with title `File changed on the Server`, and the file is untouched. A save while the workspace agent is in a turn is `409` with title `Workspace agent is working`. Writes per workspace are serialized.
- **Text only, bytes preserved.** A file is text when it has no NUL in its first 8000 bytes and decodes as strict UTF-8. A UTF-8 byte-order mark is kept on save. Line endings are whatever the client sends. A save never creates a file; it truncates and rewrites the existing one so mode and ownership stay.
- **Permissions.** Reads require `git.read` and writes require `git.write`, because Files is the same working tree Git reviews. The `workspace` claim binds these routes like every other `/api/workspaces/{workspaceId}` route.

## Clients

<DocSurfaces>
<DocSurface desktop>`Features/Files` owns the API client, `FilesPanelViewModel` (visible rows inserted under their folder on unfold) and `FileEditorViewModel`. The classic shell shows `FilesPanel` as the Files tab after Git. The command center's `agent-files` and `agent-file` scenes host the same panel in their `files` slot, and the open editor switches the scene.</DocSurface>
<DocSurface mobile>`src/features/files` owns the API provider, the pure `FileTreeProvider` and `FileEditorProvider`, `FilesScreen`, and `FileEditorScreen`. Routes are `/workspace/{id}/files` and `/workspace/{id}/files/file?path=`. The command center maps them to `mobile-agent-files` and `mobile-agent-file`; its back control asks the editor to leave so unsaved edits still prompt.</DocSurface>
</DocSurfaces>

The Demo answers the same routes from the `files` repository in `AgentUp.FakeServer/definition.json`, overlaid with each Demo workspace's Git changes. A saved Demo edit becomes a modified file in that workspace's Demo Git state.

Design system: `src/files.css` and `src/catalog-files.html` for the Old UI, and the New UI catalog's own copies with the `agent-files` and `agent-file` scenes. Rows reuse the Git change list classes; only the count, size and editor page are new.

<DocNext href="/developer-guide/git/history" title="History layout">
Paging and graph rendering for the commit log.
</DocNext>
