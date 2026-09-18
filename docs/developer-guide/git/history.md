---
title: History layout
---

# History layout

`GET /api/workspaces/{workspaceId}/git/log?max=&skip=&until=` returns a bounded newest-first commit history with parents, subject, author, timestamp, and ref decorations. `max` is the page size, clamped to 200. `skip` continues after that many newer commits in the same `git log` order. `until` is an exclusive commit object name for the same older page. The response includes `hasMore` so clients can offer Load more instead of generating the whole graph at once.

The Desktop Commit tab shows a denser Server-owned commit history with parent-lane glyphs; a labeled branch ref checks it out through the Server. On Mobile, History is graph-first: a pinned timestamp column, a horizontally scrollable graph, and a sticky header for the selected commit's message, author, branches, and tags. Checkout lives in that header.
