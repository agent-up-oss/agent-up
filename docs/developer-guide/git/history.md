---
title: History layout
---

# History layout

<DocWhat>
This page is the bounded commit log for a workspace: paging, the graph clients render, and how Desktop and Mobile differ.
</DocWhat>

<DocContract label="Route">{'GET /api/workspaces/{workspaceId}/git/log'}</DocContract>

`max` is the page size, clamped to 200. `skip` continues after that many newer commits in the same `git log` order. `until` is an exclusive commit object name for the same older page. The response includes `hasMore` so clients can offer Load more instead of generating the whole graph at once.

<DocSurfaces>
<DocSurface desktop>The Commit tab shows a denser Server-owned commit history with parent-lane glyphs; a labeled branch ref checks it out through the Server.</DocSurface>
<DocSurface mobile>History is graph-first: a pinned timestamp column, a horizontally scrollable graph, and a sticky header for the selected commit's message, author, branches, and tags. Selected chrome belongs on the timestamp only. Ref chips are labels; checkout is a confirmed header action.</DocSurface>
</DocSurfaces>
