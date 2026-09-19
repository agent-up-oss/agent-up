---
title: Review
---

# Review

<DocWhat>
Review is where you see uncommitted files, open a diff, and commit or discard only the paths you select.
</DocWhat>

Changed files are grouped by directory and indented. Directories are listed before files, and each file carries a marker for its change kind.

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

<DocSteps>
<DocStep title="Check the files you want">
Only checked files are committed. The list starts with a Changes checkbox that selects or clears every file at once.
</DocStep>
<DocStep title="Use directory checkboxes">
A directory checkbox selects or clears every file beneath it, and it shows as checked exactly when all of its files are selected. Checking a collapsed directory still selects every file under it.
</DocStep>
<DocStep title="Discard with confirmation">
Discard asks you to confirm the selected paths, then restores selected tracked files from HEAD and deletes selected untracked files.
</DocStep>
</DocSteps>

The change list refreshes on its own while the Git view is open and keeps the checkboxes for files that are still present. If the visualized tree is older than the Server tree, Mobile freezes that list and asks you to Reload before you keep selecting or committing.

## Committing

<DocSteps>
<DocStep title="Type a message">
The Commit button stays disabled until at least one file is selected and the message is not empty.
</DocStep>
<DocStep title="Confirm paths and message">
Agent-Up stages every selected path and then commits only those paths that still exist in the worktree.
</DocStep>
<DocStep title="Leave the rest untouched">
Changes to files you did not select stay in the worktree, so you can make several focused commits from one set of changes.
</DocStep>
</DocSteps>

<DocSurfaces>
<DocSurface mobile>On Mobile overview, Commit sits below the message box and shows added and deleted file counts for the selected files. Confirming a commit uses an in-app dialog.</DocSurface>
</DocSurfaces>

New files that disappeared after they were staged are dropped from the commit instead of failing the whole operation. After a successful commit the message box clears, the short commit hash is reported, and the change list reloads.

Commits use the Git identity configured for that repository or for the user running Agent-Up Server. If `user.name` and `user.email` are missing, Agent-Up returns a structured error instead of Git's raw identity help text. An in-progress merge also blocks a partial commit of selected files until you finish or abort it.

<DocCallout>
Desktop Commit and Mobile Review also show the Server-owned **agent proposal queue** when `commits.enabled` is true. That list is read-only here. Agents still enqueue through MCP. This Git surface does not replace `enqueue_commit`.
</DocCallout>
