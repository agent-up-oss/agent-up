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
