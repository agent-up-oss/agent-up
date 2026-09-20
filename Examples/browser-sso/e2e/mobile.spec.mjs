import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';
import { startBrowserSsoExample } from '../server.mjs';
import { startStaticServer } from './staticServer.mjs';

const EXPORT_DIR = process.env.AGENTUP_MOBILE_WEB_EXPORT
  ?? resolve(import.meta.dirname, '../../../AgentUp.Mobile/dist');

test('Mobile signs in through browser SSO and hides create when the document forbids it', async ({ page }) => {
  test.skip(!existsSync(EXPORT_DIR), `Mobile web export is missing at ${EXPORT_DIR}`);

  const example = await startBrowserSsoExample();
  const site = await startStaticServer(EXPORT_DIR);
  try {
    await page.goto(site.url);
    await page.getByTestId('server-url-input').fill(example.origin);
    await page.getByTestId('server-connect').click();
    await page.getByTestId('server-sso-continue').click();
    await expect(page.getByRole('heading', { name: 'Sign in to Shared Server' })).toBeVisible();
    await page.locator('#sso-continue').click();

    await expect(page.getByTestId('entitlement-card')).toBeVisible();
    await expect(page.getByText('Shared Server')).toBeVisible();
    await expect(page.getByText('This Server does not allow adding workspaces from the client.')).toBeVisible();
    await expect(page.getByTestId('shell-right-action')).toHaveCount(0);
    await expect(page.getByLabel('Add workspace')).toHaveCount(0);
  } finally {
    await site.dispose();
    await example.dispose();
  }
});
