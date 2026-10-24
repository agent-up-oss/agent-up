/**
 * Turns a validation-flow target into a Playwright locator.
 *
 * Resolution order is getByRole, then getByLabel, then getByText, then getByTestId — the same
 * semantic order Desktop export uses. Selector, X, and Y are rejected here so a fixture cannot
 * sneak a layout-coupled locator past Server save.
 */
export function locatorPlan(target) {
  if (target == null)
    throw new Error('A click or fill step needs a target.');
  if (target.selector != null || target.x != null || target.y != null)
    throw new Error('Mobile surface flows reject Selector, X, and Y locators. Address controls with role, label, name, text, or testId.');

  if (hasText(target.role)) {
    const name = firstText(target.name, target.label);
    return name
      ? { method: 'getByRole', role: target.role, name, exact: true }
      : { method: 'getByRole', role: target.role };
  }
  if (hasText(target.label))
    return { method: 'getByLabel', label: target.label, exact: true };
  if (hasText(target.name))
    return { method: 'getByRole', role: 'button', name: target.name, exact: true };
  if (hasText(target.text))
    return { method: 'getByText', text: target.text, exact: true };
  if (hasText(target.testId))
    return { method: 'getByTestId', testId: target.testId };
  throw new Error('Mobile click and fill steps require a role, label, name, text, or testId locator.');
}

export function locatorFor(page, target) {
  const plan = locatorPlan(target);
  switch (plan.method) {
    case 'getByRole':
      return plan.name
        ? page.getByRole(plan.role, { name: plan.name, exact: plan.exact })
        : page.getByRole(plan.role);
    case 'getByLabel':
      return page.getByLabel(plan.label, { exact: plan.exact });
    case 'getByText':
      return page.getByText(plan.text, { exact: plan.exact });
    case 'getByTestId':
      return page.getByTestId(plan.testId);
    default:
      throw new Error(`Unknown locator method '${plan.method}'.`);
  }
}

function hasText(value) {
  return typeof value === 'string' && value.trim().length > 0;
}

function firstText(...values) {
  return values.find(hasText);
}
