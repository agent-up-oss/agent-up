---
title: Files
---

# Files

<DocWhat>
Files shows every file in a workspace's clone, not only the ones that changed. Folders start folded and open as you unfold them. You can open any text file as its own page, read it whole, and edit it by hand.
</DocWhat>

<DocContract label="Chrome">Desktop Files tab · Mobile Files tab</DocContract>

Files sits next to Git. On Desktop it is the **Files** tab after **Git**. On Mobile it is **Files** in the bottom bar after **Git**. In the command center it is the **Files** tab in an open team member's bottom bar.

## What the tree tells you

A changed file carries the same marker it has in [Review](./review): `M` modified, `+` added, `?` untracked, `→` renamed, `!` conflicted. A folder shows how many changed files are inside it, so a folded folder still tells you where the work is.

Deleted files are not in Files, because they are no longer on disk. Git still lists them under Review.

The repository's `.git` folder is not shown. A very large folder shows its first 2000 entries and says so.

## Reading and editing a file

<DocSteps>
<DocStep title="Open the file">
Select a file name. Its page replaces the tree. Back returns to the tree with your folders still open.
</DocStep>
<DocStep title="Edit">
The editor shows the whole file with line numbers, and the line and column of the caret. Typing, selection, undo, redo, and Tab work the way they do in a text field. There is no syntax highlighting.
</DocStep>
<DocStep title="Save">
Save writes the file back. On Desktop, Ctrl+S (Cmd+S on macOS) also saves. Revert puts back the text you opened. If you leave with unsaved changes, Files asks before it discards them.
</DocStep>
</DocSteps>

<DocCallout>
If the file changed since you opened it, for example because an agent edited it, Save is refused rather than overwriting that change. Reload shows the current file. Saving is also refused while the workspace's agent is in the middle of a turn.
</DocCallout>

<DocFacts label="Limits">
<DocFact label="Binary files">Listed, but open with a note instead of an editor</DocFact>
<DocFact label="Large files">Files over 1 MB are listed, but not sent to the client or edited</DocFact>
<DocFact label="Permissions">Reading needs `git.read`; saving needs `git.write`. Without `git.write` the editor is read only</DocFact>
<DocFact label="Demo">The built-in Demo server has a small sample repository. Edits there last until you disconnect</DocFact>
</DocFacts>

A saved edit is an ordinary uncommitted change. It shows up in Review, where you commit or discard it.

<DocNext href="/docs/git/history" title="History">
Read the commits on the current branch.
</DocNext>
