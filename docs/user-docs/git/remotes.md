---
title: Branch and remotes
---

# Branch and remotes

<DocWhat>
Fetch, pull, push, and branch switch for the workspace you have open. Desktop puts branch switching on Overview; Mobile puts it on the Git tab.
</DocWhat>

<DocSurfaces>
<DocSurface desktop>Branch switching lives on the Overview tab, not in the Git view. A dropdown at the top of Overview shows the live branch, grouped local and remote-tracking branches, and ahead/behind counts against the upstream.</DocSurface>
<DocSurface mobile>Those same controls sit on the Git tab. Fetch, Pull, Push, branch switch, remote checkout, and create-branch each ask for a confirmation that names the operation and the branch or remote. The branch list shows at most five rows at a time and can be filtered. Reload sits to the left of Fetch. A History button sits next to Push. Force push (--force-with-lease) appears only when a normal push is rejected.</DocSurface>
</DocSurfaces>

<DocSteps>
<DocStep title="Switch">
Choosing a local branch switches to it. Choosing a remote-tracking branch checks it out and creates a local tracking branch when needed.
</DocStep>
<DocStep title="Create">
A `+` control opens a field to create a branch from the current HEAD.
</DocStep>
<DocStep title="Fetch, Pull, Push">
Force push is a confirmed `--force-with-lease` action, not an unconditional overwrite. On Mobile it is offered only after a rejected push, not as a toolbar button. Pull defaults to fast-forward only; if the histories have diverged, Agent-Up reports that instead of Git's raw abort text.
</DocStep>
</DocSteps>

Switching refuses to run when Git itself would refuse, such as when the worktree has conflicting changes.
