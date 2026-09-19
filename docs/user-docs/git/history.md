---
title: History
---

# History

<DocWhat>
History is a bounded commit log for the workspace. Desktop shows it on the Commit tab; Mobile opens it from the Git tab.
</DocWhat>

<DocSurfaces>
<DocSurface desktop>The Commit tab shows a bounded commit history with a simple parent-lane graph. Choosing a labeled branch ref from that history checks it out the same way as the branch dropdown.</DocSurface>
<DocSurface mobile>History is a graph-first log. Each row is a timestamp and the commit graph. Selecting a row marks the timestamp and puts the message, author, branches, and tags in a header that stays on screen while you scroll. Tapping a branch chip does not switch branches. Checkout is a confirmed action in that header. The first page is the newest 200 commits; Load more fetches the next older page. Inner pages return through the nav-bar back button.</DocSurface>
</DocSurfaces>
