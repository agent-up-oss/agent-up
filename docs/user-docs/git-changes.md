---
title: Git changes
---

# Git changes

Every workspace has a Git view that shows the uncommitted changes in its worktree and lets you commit a chosen subset of them.

Open it from the Git icon in the Desktop title bar, which reveals a panel on the right of the selected workspace. On Mobile it is the Git tab, which follows the workspace selected on the Workspaces tab.

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

Each file has a checkbox, and only checked files are committed. A directory checkbox selects or clears every file beneath it at once, and it shows as checked exactly when all of its files are selected. The header above the list shows how many of the workspace's changed files are currently selected.

## Committing

Type a message in the box below the list and choose Commit. The button stays disabled until at least one file is selected and the message is not empty.

Agent-Up stages and commits only the selected paths. Changes to files you did not select stay in the worktree untouched, so you can make several focused commits from one set of changes. After a successful commit the message box clears, the short commit hash is reported, and the change list reloads.

Commits use the Git identity configured for that repository or for the user running Agent-Up Server. If no identity is configured, Git rejects the commit and Agent-Up shows the reason.

## Related

- [Workspace](./workspace.md)
