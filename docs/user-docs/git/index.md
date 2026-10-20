---
title: Git
---

<DocEyebrow slice="Git" status="available" />

# Git

<DocWhat>
Git is the human working-tree review surface. Every workspace has a view of uncommitted changes, per-file diffs, a chosen-path commit, branch fetch/pull/push, and a bounded history.

Desktop chrome is the **Git** tab, heading **Git changes**. Mobile chrome is the **Git** tab, with inner **Review** and **History** pages.
</DocWhat>

<DocCallout>
Agents still enqueue through the commit queue. This slice does not replace `enqueue_commit`.
</DocCallout>

<DocSpine>
<DocBeat>Open Review</DocBeat>
<DocBeat>Select paths and commit</DocBeat>
<DocBeat>Use History or remotes when you need them</DocBeat>
</DocSpine>

<DocContract label="Chrome">Desktop Git · Mobile Git</DocContract>

<DocSurfaces>
<DocSurface desktop>Open it from the Git tab of the selected workspace. Branch switching, fetch, pull, and push live at the top of that tab. History is an inner page from the History button. Those write controls stay hidden when the Server does not allow `git.write`.</DocSurface>
<DocSurface mobile>Open it from the Git tab. The overview lists uncommitted changes; a filename opens the file viewer. History sits next to Push. Inner Review and History pages return through the nav-bar back button. Fetch, pull, push, discard, and commit stay hidden when the Server does not allow `git.write`.</DocSurface>
</DocSurfaces>

When a hosting module is enabled, Desktop Git shows a strip above the change tree with the open proposal, check state, review threads, and mergeability. Mobile shows the same facts as a row on the Git overview, with thread links on Review.

<p><img src="/desktop-git.png" alt="Desktop Git changes showing ProductGrid.tsx" /></p>
<p><img src="/mobile-git.png" alt="Mobile Git tab showing ProductGrid.tsx" /></p>

<DocNext href="/docs/git/review" title="Review">
Choose paths and commit them.
</DocNext>
