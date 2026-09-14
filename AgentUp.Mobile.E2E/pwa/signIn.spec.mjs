import { expect, test } from '@playwright/test';

import { startStack } from '../harness/stack.mjs';
import { SIGN_IN_FLOWS, waitForAgentState, waitForChallenge } from '../harness/signInFlows.mjs';
import { startStaticServer } from '../harness/staticServer.mjs';

/**
 * The same four sign-in shapes, driven through the installable web build of the same client.
 *
 * This is the variant where the redirect transport behaves differently: a browser cannot hand a
 * loopback redirect back the way an in-app WebView can, so the web adapter watches a popup it
 * opened. Covering both here is what keeps the two adapters honest about the same contract.
 */
const SCENARIOS = [
  { name: 'device code', flow: 'device', codexSchema: 'device', kind: 'Codex', transport: 'code' },
  { name: 'pasted code', flow: 'paste', codexSchema: 'device', kind: 'Claude', transport: 'code' },
  { name: 'poll until approved', flow: 'poll', codexSchema: 'device', kind: 'Cursor', transport: 'poll' },
  { name: 'loopback redirect', flow: 'redirect', codexSchema: 'redirect', kind: 'Codex', transport: 'redirect' },
];

const EXPORT_DIR = process.env.AGENTUP_E2E_WEB_EXPORT ?? '../AgentUp.Mobile/dist';

for (const scenario of SCENARIOS) {
  test.describe(scenario.name, () => {
    let stack;
    let site;

    test.beforeAll(async () => {
      stack = await startStack({
        platform: 'web',
        codexSchema: scenario.codexSchema,
        serverDll: process.env.AGENTUP_E2E_SERVER_DLL,
        testAgentExecutable: process.env.AGENTUP_E2E_TEST_AGENT,
      });
      site = await startStaticServer(EXPORT_DIR);
    });

    test.afterAll(async () => {
      await site?.dispose();
      await stack?.dispose();
    });

    test.beforeEach(async ({ context }) => {
      await stack.control.reset();
      await context.clearCookies();
    });

    test('signs the agent in and leaves the session ready', async ({ page }) => {
      const flow = SIGN_IN_FLOWS[scenario.flow];
      await flow.beforeStart?.({ control: stack.control });

      await page.goto(site.url);
      await page.getByTestId('server-url-input').fill(stack.serverOriginForClient);
      await page.getByTestId('server-connect').click();

      // Workspaces are chosen from the shell's sidebar, which starts closed, and land on the
      // workspace dashboard. The agent chat is one step further in.
      await page.getByTestId('open-sidebar').click();
      await page.getByTestId(`workspace-${stack.workspace.id}`).click();
      await page.getByTestId('open-workspace-agent').click();
      await page.getByTestId(`agent-picker-${scenario.kind}`).click();

      const offered = await waitForAgentState(stack.serverUrl, stack.workspace.id, 'authentication_required');
      const methodId = offered.authMethods?.[0]?.id;
      expect(methodId, 'the agent must offer a subscription sign-in method').toBeTruthy();
      await page.getByTestId(`agent-auth-method-${methodId}`).click();

      const session = await waitForChallenge(
        stack.serverUrl,
        stack.workspace.id,
        challenge => challenge.url !== null && challenge.transport === scenario.transport,
      );

      // The sign-in opens in a new tab. For the redirect shape the browser cannot hand the
      // callback back — it is on another origin — so nothing is posted: the redirect reaches the
      // agent CLI's own listener directly, because this browser is on the Server's host.
      const opened = page.waitForEvent('popup').catch(() => null);
      await page.getByTestId('agent-signin-open').click();
      await opened;

      const carriedCode = await flow.approve?.({ control: stack.control, session });
      if (carriedCode) {
        await page.getByTestId('agent-signin-code-input').fill(carriedCode);
        await page.getByTestId('agent-signin-submit-code').click();
      }

      await waitForAgentState(stack.serverUrl, stack.workspace.id, 'ready', { timeoutMs: 150_000 });
    });
  });
}
