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

The Server persists flow definitions in the workspace project at `.agent-up/validation-flows.json`, so validation checks are versioned with the repository. Supplying an existing flow ID replaces it as a new version, which supports editing and re-recording without creating duplicates. The Desktop Validation sidebar lists checks for the selected application, expands each flow into its steps and assertions while replay runs, and shows pass or fail icons for individual checks, stages, and the overall flow. Replay plays in the embedded WebView with staged mouse movement, half-second attention pings, navigation, and page-load waits so the user can follow the journey.

Playwright export prefers role, accessible name, label, test ID, and visible text locators in that order; CSS is only an export fallback; click and fill steps retain a stable selector fallback so the same flow can also replay in the Desktop WebView. Agents should infer routes and controls from application source or router files when that is quicker than inspecting every page, perform the journey once, then call `save_validation_flow` with user-meaningful descriptions and visible expectations after each step. Navigation is restricted to single-slash application-relative paths and exports resolve them against `AGENT_UP_BASE_URL`, preventing a recorded flow from escaping to another origin while allowing the same check to use dynamically allocated local ports and a CI-provided headless target. Assertions belong after the action whose user-visible outcome they describe, rather than relying on sleeps or incidental implementation state.
