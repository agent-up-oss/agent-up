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

Desktop Git always shows a hosting strip above the change tree. With no forge module enabled it names that cause and the bootstrap remedy. After connection it shows the open proposal, check state, review threads, and mergeability. Mobile shows the same facts as a row on the Git overview, with thread links on Review.

## Enable a repository host

GitHub and Forgejo are first-party forge modules. Enable the module matching the repository remote, then connect it from the Git hosting strip. Host credentials stay on the Server; they never go in `agent-up.json`.

<DocSteps>
<DocStep title="Pack and enable">Run `agent-up capabilities bootstrap`, or enable `github` or `forgejo` from Settings. With neither enabled the strip shows `notInstalled` and `Run: agent-up capabilities bootstrap`.</DocStep>
<DocStep title="Point at the host">GitHub defaults to github.com and needs no client id or client secret. For GitHub Enterprise set `AGENTUP_GITHUB_API_URL` and `AGENTUP_GITHUB_AUTHORIZE_URL`. For Forgejo set `AGENTUP_FORGEJO_ORIGIN` (API and OAuth URLs are derived); override with `AGENTUP_FORGEJO_API_URL` when the instance is not at `/api/v1`.</DocStep>
<DocStep title="Connect GitHub">Use Connect on the workspace's Git hosting strip. GitHub asks you to create the prefilled private app and install it on the repository inferred from that workspace's `origin`. Complete this once in every workspace: each gets a separate identity such as `Agent-Up-Workspace-1-a1b2c3d4`.</DocStep>
<DocStep title="Connect Forgejo">Create an OAuth application on the Forgejo instance, set its callback to the Server's `/api/forge/callback`, then configure `AGENTUP_FORGE_CLIENT_ID`, `AGENTUP_FORGE_CLIENT_SECRET`, and `AGENTUP_FORGE_REDIRECT_URI`. Use Connect on the workspace's Git hosting strip.</DocStep>
</DocSteps>

The callback URL displayed by GitHub must reach the same Server URL used to start the connection. Packaged local use works at `http://localhost:5000`; when the Server is remote or behind a proxy, connect the client through its browser-reachable public URL before starting setup.

For an organization-owned repository, GitHub creates the private app under that organization, so the person completing setup must be allowed to register and install GitHub Apps there. For a personal repository, GitHub creates it under that user account. You do not create an OAuth app or copy a client secret into Agent-Up in either case.

<p><img src="/desktop-git.png" alt="Desktop Git changes showing ProductGrid.tsx" /></p>
<p><img src="/mobile-git.png" alt="Mobile Git tab showing ProductGrid.tsx" /></p>

<DocNext href="/docs/git/review" title="Review">
Choose paths and commit them.
</DocNext>
