---
title: Git changes
---

# Git changes

Every workspace has a Git view that shows the uncommitted changes in its worktree and lets you commit a chosen subset of them.

Open it from the Commit tab of the selected Desktop workspace. On Mobile it is the Git tab of the selected workspace. Branch switching lives on the Desktop Overview tab and the Mobile Git tab.

The Mobile Git tab shows the live branch, grouped local and remote-tracking branches, Fetch/Pull/Push, a compact working-tree commit section, and a History summary. Review opens the full change list. View history opens the commit graph. Both inner pages return through the nav-bar back button. See [Mobile](./mobile.md).

Desktop Commit and Mobile Review also show the Server-owned **agent proposal queue** when `commits.enabled` is true. That list is read-only here: entry order, messages, and verification states come from `GET /api/workspaces/{id}/commit-queue`. Agents still enqueue through MCP. This Git surface does not replace `enqueue_commit`.

## Reading the change list

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

Each file has a checkbox, and only checked files are committed. The list starts with a Changes checkbox that selects or clears every file at once. A directory checkbox selects or clears every file beneath it, and it shows as checked exactly when all of its files are selected.

The change list refreshes on its own while the Git view is open and keeps the checkboxes for files that are still present. Discard asks you to confirm the selected paths, then restores selected tracked files from HEAD and deletes selected untracked files.

On Desktop, branch switching lives on the Overview tab, not in the Git view. A dropdown at the top of Overview shows the live branch, grouped local and remote-tracking branches, and ahead/behind counts against the upstream. Choosing a local branch switches to it. Choosing a remote-tracking branch checks it out and creates a local tracking branch when needed. A `+` control opens a field to create a branch from the current HEAD. Fetch, Pull, and Push sit next to the dropdown. Force push is a confirmed `--force-with-lease` action, not an unconditional overwrite. Switching refuses to run when Git itself would refuse, such as when the worktree has conflicting changes. Pull defaults to fast-forward only. On Mobile those same controls sit on the Git tab.

On Desktop, the Commit tab also shows a bounded commit history with a simple parent-lane graph. Choosing a labeled branch ref from that history checks it out the same way as the dropdown. On Mobile, that graph lives on the Git History page.

## Committing

Type a message in the box below the list and choose Commit. The button stays disabled until at least one file is selected and the message is not empty.

Agent-Up stages every selected path with `git add` and then commits only those paths that still exist in the worktree. New files that disappeared after they were staged are dropped from the commit instead of failing the whole operation. Changes to files you did not select stay in the worktree untouched, so you can make several focused commits from one set of changes. After a successful commit the message box clears, the short commit hash is reported, and the change list reloads.

Commits use the Git identity configured for that repository or for the user running Agent-Up Server. If no identity is configured, Git rejects the commit and Agent-Up shows the reason.

## Related

- [Workspace](./workspace.md)
