---
title: Review
---

# Review

Changed files are grouped by directory and indented, the same way a commit window shows a project in directory mode. Directories are listed before files, and each file carries a marker for its change kind:

| Marker | Meaning |
|--------|---------|
| `M` | Modified |
| `+` | Added |
| `?` | Untracked |
| `−` | Deleted |
| `→` | Renamed |
| `!` | Conflicted |

Each untracked file gets its own row under its directory. Git alone would collapse a new directory into a single entry, so listing the files individually lets you select exactly the new files you want.

Selecting a file name opens its diff in a modal. Close the modal to return to the list. Binary files report that no text diff is rendered instead of showing raw bytes.

## Choosing what to commit

Each file has a checkbox, and only checked files are committed. The list starts with a Changes checkbox that selects or clears every file at once. A directory checkbox selects or clears every file beneath it, and it shows as checked exactly when all of its files are selected. Directory triangles collapse and expand nested rows; checking a collapsed directory still selects every file under it.

The change list refreshes on its own while the Git view is open and keeps the checkboxes for files that are still present. Discard asks you to confirm the selected paths, then restores selected tracked files from HEAD and deletes selected untracked files.

## Committing

Type a message in the box below the list and choose Commit. The button stays disabled until at least one file is selected and the message is not empty. Commit asks you to confirm the selected paths and the message. On Mobile overview, Commit sits below the message box and shows added and deleted file counts.

Agent-Up stages every selected path with `git add` and then commits only those paths that still exist in the worktree. New files that disappeared after they were staged are dropped from the commit instead of failing the whole operation. Changes to files you did not select stay in the worktree untouched, so you can make several focused commits from one set of changes. After a successful commit the message box clears, the short commit hash is reported, and the change list reloads.

Commits use the Git identity configured for that repository or for the user running Agent-Up Server. If no identity is configured, Git rejects the commit and Agent-Up shows the reason.

Desktop Commit and Mobile Review also show the Server-owned **agent proposal queue** when `commits.enabled` is true. That list is read-only here: entry order, messages, and verification states come from the Server. Agents still enqueue through MCP. This Git surface does not replace `enqueue_commit`.
