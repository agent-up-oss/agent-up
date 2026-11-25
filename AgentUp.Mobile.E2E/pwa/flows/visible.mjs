/**
 * Visible waits for Mobile chrome must bring the match into view.
 *
 * Playwright's `visible` state does not scroll. Files lists the clone with directories first, so
 * a top-level file such as README.md sits below the fold on a phone viewport. Click already
 * scrolls; a Visible expectation that does not would wait out its budget on a control that is
 * already in the document.
 */
export async function waitForVisible(locator, { timeout = 180_000 } = {}) {
  await locator.waitFor({ state: 'attached', timeout });
  await locator.scrollIntoViewIfNeeded();
  await locator.waitFor({ state: 'visible', timeout });
}
