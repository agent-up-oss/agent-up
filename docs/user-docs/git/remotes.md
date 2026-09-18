---
title: Branch and remotes
---

# Branch and remotes

On Desktop, branch switching lives on the Overview tab, not in the Git view. A dropdown at the top of Overview shows the live branch, grouped local and remote-tracking branches, and ahead/behind counts against the upstream. Choosing a local branch switches to it. Choosing a remote-tracking branch checks it out and creates a local tracking branch when needed. A `+` control opens a field to create a branch from the current HEAD. Fetch, Pull, and Push sit next to the dropdown. Force push is a confirmed `--force-with-lease` action, not an unconditional overwrite. Switching refuses to run when Git itself would refuse, such as when the worktree has conflicting changes. Pull defaults to fast-forward only.

On Mobile those same controls sit on the Git tab. Fetch, Pull, Push, Force push, branch switch, remote checkout, and create-branch each ask for a confirmation that names the operation and the branch or remote. The branch list shows at most five rows at a time and can be filtered. A History button sits next to Force push.
