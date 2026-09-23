---
title: Remotes and mutations
---

# Remotes and mutations

<DocWhat>
This page is fetch, pull, push, and branch switch for a workspace worktree. The Server runs the Git operations; clients only display the result.
</DocWhat>

<DocSteps>
<DocStep title="POST .../git/branch">
Switches to a local branch or creates a new branch from the current HEAD.
</DocStep>
<DocStep title="POST .../git/checkout">
Creates a local tracking branch from a unique remote-tracking ref, or switches to the local branch when it already exists.
</DocStep>
<DocStep title="POST .../git/fetch">
Runs `git fetch --prune`, optionally for one remote, with `GIT_TERMINAL_PROMPT=0`.
</DocStep>
<DocStep title="POST .../git/pull">
Runs `git pull --ff-only`, or `git pull --rebase` when requested.
</DocStep>
<DocStep title="POST .../git/push">
Runs `git push`, optionally `--force-with-lease` and `-u`.
</DocStep>
</DocSteps>

Successful branch switch, remote checkout, and pull refresh the workspace registry's live branch and commit so sidebar and overview identity match HEAD. Remote mutations reuse the same per-workspace lock and reject while an ACP prompt is running.

Auth failures, a missing upstream, a non-fast-forward pull, a stale force-with-lease, and a dirty worktree switch return structured problem details rather than raw Git stderr.

<DocSurfaces>
<DocSurface desktop>Branch switching lives at the top of the Git tab, not on Overview. The Git tab also hosts Fetch, Pull, Push, and a confirmed force-with-lease action, and lists remote-tracking branches beside local ones. History is an inner page from the History button.</DocSurface>
<DocSurface mobile>Those same controls sit on the Git tab. Every fetch, pull, push, switch, remote checkout, and create-branch mutation asks for an explicit confirmation that names the branch or remote. Force-with-lease is offered only from the result of a rejected non-fast-forward push.</DocSurface>
</DocSurfaces>
