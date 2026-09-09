---
title: Playwright Generation
---

# Playwright Generation

Agent-Up can generate Playwright tests from recorded interaction history.

Generated tests should:

- Prefer semantic locators.
- Avoid brittle selectors.
- Generate assertions.
- Produce readable code.
- Follow Playwright best practices.

## Workflow Inference

Agent-Up should infer user intent instead of exporting raw interaction history.

Example interaction:

- Open Orders.
- Create Customer.
- Add Products.
- Submit Order.
- Verify Success.

Generated test:

```text
Creating an order succeeds
```

The output should not look like:

```text
Click Button 17
```

## Automatic Assertions

Agent-Up should infer assertions such as:

- Success notification visible.
- Navigation completed.
- Validation error visible.
- Button disabled.
- URL changed.
- Network request completed.

Generated tests should validate outcomes rather than merely replay interactions.

## Behavioral validation flows

A validation flow belongs to one workspace application and starts at an application-relative initial path. It records the meaningful route through the GUI plus an observable expectation at the initial place and after each step. Agents must describe user intent and visible behavior—not DOM structure, framework components, backend calls, or other implementation detail.

The Server persists flow definitions beside its workspace repository data. Supplying an existing flow ID replaces it as a new version, which supports editing and re-recording without creating duplicates. The Desktop Validation drawer lists checks for the selected application and plays them through the shared browser so the user can watch.

Playwright export prefers role, accessible name, label, test ID, and visible text locators in that order; CSS is only an export fallback; click and fill steps retain a stable selector fallback so the same flow can also replay in the shared Desktop browser. Exports resolve relative navigation against `AGENT_UP_BASE_URL`, allowing the same check to use dynamically allocated local ports and a CI-provided headless target. Assertions belong after the action whose user-visible outcome they describe, rather than relying on sleeps or incidental implementation state.
