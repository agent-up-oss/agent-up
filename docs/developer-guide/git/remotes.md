---
title: Remotes and mutations
---

# Remotes and mutations

- `POST /api/workspaces/{workspaceId}/git/branch` switches to a local branch or creates a new branch from the current HEAD.
- `POST /api/workspaces/{workspaceId}/git/checkout` creates a local tracking branch from a unique remote-tracking ref, or switches to the local branch when it already exists.
- `POST /api/workspaces/{workspaceId}/git/fetch` runs `git fetch --prune`, optionally for one remote, with `GIT_TERMINAL_PROMPT=0`.
- `POST /api/workspaces/{workspaceId}/git/pull` runs `git pull --ff-only`, or `git pull --rebase` when requested.
- `POST /api/workspaces/{workspaceId}/git/push` runs `git push`, optionally `--force-with-lease` and `-u`.

Successful branch switch, remote checkout, and pull refresh the workspace registry's live branch and commit so sidebar and overview identity match HEAD. Remote mutations reuse the same per-workspace lock and reject while an ACP prompt is running.

On Desktop, branch switching lives on the Overview tab, not in the Git panel. Overview also hosts Fetch, Pull, Push, and a confirmed force-with-lease action, and lists remote-tracking branches beside local ones. On Mobile those same controls sit on the Git tab, and every fetch, pull, push, force-with-lease, switch, remote checkout, and create-branch mutation asks for an explicit confirmation that names the branch or remote.
