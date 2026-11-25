/**
 * Visible waits for Mobile chrome must bring the match into view.
 *
 * Playwright's `visible` state does not scroll. Files lists the clone with directories first, so
 * a top-level file such as README.md sits below the fold on a phone viewport. Click already
 * scrolls; a Visible expectation that does not would wait out its budget on a control that is
 * already in the document.
 *
 * The first currently-visible match is also often the previous Expo Router screen. That node
 * detaches while Settings (or Files) is still laying out, and a one-shot scroll then throws
 * "Element is not attached to the DOM". Retry until the locator stays attached through the
 * scroll. Every attempt, including scroll, uses the same deadline.
 */
import { waitFor } from '../../harness/wait.mjs';

export async function waitForVisible(locator, { timeout = 180_000 } = {}) {
  await waitFor('the match to stay attached while scrolling into view', async () => {
    await locator.waitFor({ state: 'attached', timeout });
    await locator.scrollIntoViewIfNeeded({ timeout });
    await locator.waitFor({ state: 'visible', timeout });
    return true;
  }, { timeoutMs: timeout });
}
