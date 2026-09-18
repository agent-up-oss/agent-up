---
title: Git
---

<DocEyebrow slice="Git" status="available" />

# Git

<DocFocus>
Git is the human working-tree review surface. Agents still enqueue through the commit queue; this slice does not replace `enqueue_commit`.
</DocFocus>

## What it is

Every workspace has a Git view of uncommitted changes. It can commit a chosen subset of paths, switch branches, fetch/pull/push, and show a bounded history. Desktop chrome is the **Commit** tab; the heading inside it is **Git changes**. Mobile chrome is the **Git** tab, with inner **Review** and **History** pages.

<DocSpine>
<DocBeat selected>Open Review</DocBeat>
<DocBeat>Select paths and commit</DocBeat>
<DocBeat>Use History or remotes when you need the graph or Fetch/Pull/Push</DocBeat>
</DocSpine>

<DocContract>Commit tab · Git tab</DocContract>

<DocSurface desktop>Open it from the Commit tab of the selected workspace. Branch switching lives on the Overview tab.</DocSurface>

<DocSurface mobile>Open it from the Git tab. The overview lists uncommitted changes; a filename opens the file viewer. History sits next to Force push and opens the commit graph. Inner Review and History pages return through the nav-bar back button.</DocSurface>

Desktop Commit and Mobile Review also show the Server-owned **agent proposal queue** when `commits.enabled` is true. That list is read-only here. See [Commits](/docs/commits).

Next in this slice: [Review](/docs/git/review).
