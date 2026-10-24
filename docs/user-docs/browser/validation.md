---
title: Validation
---

# Validation

<DocWhat>
A validation check is a user-meaningful route through the GUI, not an implementation walkthrough. Most checks run in an application view. Checks can also name Agent-Up Desktop itself — connect, workspaces, Git, Agents — using stable control ids rather than a page in an app.
</DocWhat>

<DocSteps>
<DocStep title="Open Validation">
Select an application in a Desktop workspace. The Validation sidebar appears on the right and starts collapsed.
</DocStep>
<DocStep title="Play the check">
Application checks play in the Desktop application view so you can watch the recorded steps and their expected outcomes. Checks that name Agent-Up chrome itself replay on the Desktop window through `au-debug validate --surface desktop`.
</DocStep>
<DocStep title="Edit or export">
Agents can edit or re-record an existing check without losing its identity, and export it as a Playwright test for headless CI.
</DocStep>
</DocSteps>
