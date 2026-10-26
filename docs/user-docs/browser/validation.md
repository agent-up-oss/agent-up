---
title: Validation
---

# Validation

<DocWhat>
A validation check is a user-meaningful route through the GUI, not an implementation walkthrough. Most checks run in an application view. Checks can also name Agent-Up Desktop or Mobile chrome — connect, workspaces, Git, Agents — using stable control names rather than a page in an app.
</DocWhat>

<DocSteps>
<DocStep title="Open Validation">
Select an application. Desktop shows a Validation sidebar on the right that starts collapsed. Mobile HTTP application pages show a Navbar checkmark that opens the same checks as a right bar.
</DocStep>
<DocStep title="Play the check">
Application checks play in the Desktop application view so you can watch the recorded steps and their expected outcomes. Checks that name Agent-Up chrome itself replay through `au-debug validate --surface desktop` or `--surface mobile`.
</DocStep>
<DocStep title="Edit or export">
Agents can edit or re-record an existing check without losing its identity, and export it as a Playwright test for headless CI.
</DocStep>
</DocSteps>
