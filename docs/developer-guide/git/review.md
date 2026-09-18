---
title: Review tree and diffs
---

# Review tree and diffs

- `GET /api/workspaces/{workspaceId}/git/changes` returns the uncommitted changes as a directory tree with per-file status, the live branch name, local and remote-tracking branch names, upstream, and ahead/behind counts.
- `GET /api/workspaces/{workspaceId}/git/head` returns the live branch, local and remote-tracking branches, upstream, ahead/behind, and HEAD commit without the change tree.
- `GET /api/workspaces/{workspaceId}/git/file?path=` returns one file's diff, including untracked files.
- `POST /api/workspaces/{workspaceId}/git/commit` stages and commits only the requested paths with the supplied message and returns the new commit.
- `POST /api/workspaces/{workspaceId}/git/discard` restores selected tracked files from HEAD and deletes selected untracked files.

Commit uses `git commit --only` after staging the selected files that still exist. New files that vanished after they were staged are unstaged instead of failing the whole commit. Because the commit passes explicit pathspecs, changes to files the caller did not select stay in the worktree.

Desktop `GitPanelViewModel` owns selection propagation between directory and file rows. Directory chevrons collapse nested rows in client state; a collapsed directory checkbox still selects every file beneath it. Selecting a file name opens its diff in a modal. The panel reloads whenever the selected workspace changes, after a successful commit or discard, and on a short poll while it is open. The same refresh requests `/api/workspaces/{workspaceId}/commit-queue` and displays ordered proposal entries. Desktop treats the returned generation, ancestry, and managed-worktree path as authoritative and does not reconstruct queue state from the working tree.

Mobile `src/features/git/` owns the Git overview tab, Review page, History page, and the workspace branch picker. Tree flattening, directory/file selection, collapse visibility, branch-picker search, and log-lane layout stay pure functions in `providers/` so they are covered by node tests without a renderer. The Git tab overview lists changes and commits from the same tree; it does not have a Review button. History is a top action next to Force push. The branch picker filters local and remote names and shows five rows at a time. The Review page displays Server-owned proposal queue messages, generation, and verification states; Mobile never derives ancestry or readiness locally.
