import { expect, test } from '@playwright/test';

import { startStaticServer } from '../harness/staticServer.mjs';

/**
 * The chat mounts at all.
 *
 * This needs no Server, and that is the point: it catches the failures that happen before any
 * sign-in could start. The one that prompted it was a second copy of react underneath the chat
 * module - resolved ahead of the app's because the module is a file: dependency - which left every
 * hook in the module reading a different dispatcher than the app rendered with. The connect screen
 * still appeared, so the suite looked fine right up until the first scenario timed out on a screen
 * that had thrown. Here it costs a couple of seconds and names itself.
 */
const EXPORT_DIR = process.env.AGENTUP_E2E_WEB_EXPORT ?? '../AgentUp.Mobile.E2E.App/dist';

test('the chat module mounts without a Server', async ({ page }) => {
  const site = await startStaticServer(EXPORT_DIR);
  const thrown = [];
  page.on('pageerror', error => thrown.push(String(error)));

  try {
    await page.goto(site.url);
    await page.getByTestId('server-url-input').fill('http://127.0.0.1:1');
    await page.getByTestId('workspace-id-input').fill('not-a-real-workspace');
    await page.getByTestId('server-connect').click();

    // It cannot reach that Server, so it says so. Reaching the point of saying so is the assertion.
    await expect(page.getByTestId('agent-picker-Codex')).toBeVisible({ timeout: 30_000 });
    expect(thrown, 'the chat module threw while mounting').toEqual([]);
  } finally {
    await site.dispose();
  }
});
