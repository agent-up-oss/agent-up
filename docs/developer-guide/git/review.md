---
title: Review tree and diffs
---

# Review tree and diffs

<DocWhat>
This page is the change tree, per-file diffs, and the selective commit and discard routes for a workspace worktree.
</DocWhat>

<DocSteps>
<DocStep title="GET .../git/changes">
Returns the uncommitted changes as a directory tree with per-file status, the live branch name, local and remote-tracking branch names, upstream, and ahead/behind counts.
</DocStep>
<DocStep title="GET .../git/head">
Returns the live branch, local and remote-tracking branches, upstream, ahead/behind, and HEAD commit without the change tree.
</DocStep>
<DocStep title="GET .../git/file?path=">
Returns one file's diff, including untracked files.
</DocStep>
<DocStep title="POST .../git/commit">
Stages and commits only the requested paths with the supplied message and returns the new commit.
</DocStep>
<DocStep title="POST .../git/discard">
Restores selected tracked files from HEAD and deletes selected untracked files.
</DocStep>
</DocSteps>

Commit uses `git commit --only` after staging the selected files that still exist. New files that vanished after they were staged are unstaged instead of failing the whole commit. Because the commit passes explicit pathspecs, changes to files the caller did not select stay in the worktree. Missing Git identity, an in-progress merge, and other Git failures return structured problem details rather than raw Git stderr.

<DocSurfaces>
<DocSurface desktop>Desktop owns selection propagation between directory and file rows. Directory chevrons collapse nested rows in client state; a collapsed directory checkbox still selects every file beneath it. Directory names and chevrons are muted grouping chrome without a painted background. Selecting a file name opens its diff in a modal. Discard sits next to Push and appears only when files are selected. When `git.write` is unavailable the commit box, discard, branch create, fetch, pull, and push stay hidden. The panel reloads whenever the selected workspace changes, after a successful commit or discard, and on a short poll while it is open. The same refresh requests the commit queue and displays ordered proposal entries. Desktop treats the returned generation, ancestry, and managed-worktree path as authoritative and does not reconstruct queue state from the working tree. History is an inner Git-tab page from the History button, not a log under the commit box.</DocSurface>
<DocSurface mobile>Mobile `src/features/git/` owns the Git overview tab, Review page, History page, and the workspace branch picker. Tree flattening, directory/file selection, collapse visibility, selected-file counts, stale-tree signatures, branch-picker search, outside-press dismiss, and log-lane layout stay pure functions in `providers/` so they are covered by node tests without a renderer. The Git tab overview lists changes and commits from the same tree; it does not have a Review button. The overview scrolls the branch picker, its actions, the hosting row, and the commit box together, and the change list rests in `GitChangeSheet` on the page's bottom edge: a peek of a quarter of the page (at least 168pt) whose rows stay tappable, and the full page. There is no half detent and no closed state; `GitChangeSheetProvider` owns the detents, drag ownership, and settling. Inside a shell `SheetLayer` - the command center's member sheet - the page keeps only an anchor and the sheet is drawn in the layer: it rests on the page's bottom edge above the member's app bar and opens over the whole layer, head and app bar included. `SheetLayerProvider` owns that frame, and the layer reports an open inner sheet so the member sheet leaves a drag down to it. `gitChangesUseSheet` keeps the sheet to a phone-width window, the same 430pt `usesSheet` rule the chat uses; anything wider lists the changes in a scrolling pane under the commit box. History is a top action next to Push. Discard appears next to Push only when files are selected. When `git.write` is unavailable those write controls stay hidden and the branch picker is read-only. Polling does not silently replace a visualized tree whose file set or statuses differ from the Server; Reload applies the current tree. The Review page displays Server-owned proposal queue messages, generation, and verification states; Mobile never derives ancestry or readiness locally.</DocSurface>
</DocSurfaces>
