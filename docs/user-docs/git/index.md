---
title: Git
---

<DocEyebrow slice="Git" status="available" />

# Git

<DocWhat>
Git is the human working-tree review surface. Every workspace has a view of uncommitted changes, per-file diffs, a chosen-path commit, branch fetch/pull/push, and a bounded history.

Desktop chrome is the **Commit** tab, heading **Git changes**. Mobile chrome is the **Git** tab, with inner **Review** and **History** pages.
</DocWhat>

<DocCallout>
Agents still enqueue through the commit queue. This slice does not replace `enqueue_commit`.
</DocCallout>

<DocSpine>
<DocBeat>Open Review</DocBeat>
<DocBeat>Select paths and commit</DocBeat>
<DocBeat>Use History or remotes when you need them</DocBeat>
</DocSpine>

<DocContract label="Chrome">Desktop Commit · Mobile Git</DocContract>

<DocSurfaces>
<DocSurface desktop>Open it from the Commit tab of the selected workspace. Branch switching lives on the Overview tab.</DocSurface>
<DocSurface mobile>Open it from the Git tab. The overview lists uncommitted changes; a filename opens the file viewer. History sits next to Push. Inner Review and History pages return through the nav-bar back button.</DocSurface>
</DocSurfaces>

<DocNext href="/docs/git/review" title="Review">
Choose paths and commit them.
</DocNext>
