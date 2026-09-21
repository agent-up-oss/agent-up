/**
 * Agent sign-in, end to end, on a real simulator and a real emulator.
 *
 * The app under test is the chat harness: the real chat module and the real sign-in module with
 * no shell around them. Every line these tests drive is a line the shipping client runs.
 *
 * A real Agent-Up Server process, the real test agent CLIs as separate processes on a PATH the
 * Server resolves them from, and a real OAuth identity provider behind them. The only thing
 * standing in for a person is the approval, which the test performs through the provider's
 * control plane at a moment it chooses, so nothing here waits out a polling interval or drives a
 * browser's DOM.
 *
 * Every shape is driven through the same client code, because the client branches on the
 * transport the Server reports and never on which agent is signing in.
 */
const SERVER_DLL = process.env.AGENTUP_E2E_SERVER_DLL;
const TEST_AGENT = process.env.AGENTUP_E2E_TEST_AGENT;
const { connectLaunchUrl } = require('../../AgentUp.Mobile.E2E.App/src/connectLaunch');

// Named here rather than imported, so the matrix is readable without opening the harness.
const SCENARIOS = [
  { name: 'device code', flow: 'device', codexSchema: 'device', agentId: 'codex', transport: 'code' },
  { name: 'pasted code', flow: 'paste', codexSchema: 'device', agentId: 'claude', transport: 'code' },
  { name: 'poll until approved', flow: 'poll', codexSchema: 'device', agentId: 'cursor', transport: 'poll' },
  { name: 'loopback redirect', flow: 'redirect', codexSchema: 'redirect', agentId: 'codex', transport: 'redirect' },
];

describe('agent sign-in', () => {
  let harness;

  beforeAll(async () => {
    // The harness is ESM and this runner is CommonJS, so it is brought in dynamically. That
    // needs Node's VM module support, which the test:ios and test:android scripts turn on -
    // without it every scenario fails here, before its body ever runs.
    const [stack, flows] = await Promise.all([
      import('../harness/stack.mjs'),
      import('../harness/signInFlows.mjs'),
    ]);
    harness = { ...stack, ...flows };
  });

  describe.each(SCENARIOS)('$name', scenario => {
    let stack;

    beforeAll(async () => {
      stack = await harness.startStack({
        platform: device.getPlatform(),
        codexSchema: scenario.codexSchema,
        serverDll: SERVER_DLL,
        testAgentExecutable: TEST_AGENT,
      });
    });

    afterAll(async () => {
      await stack?.dispose();
    });

    beforeEach(async () => {
      await stack.control.reset();
      // Fresh app state per case: a credential left over from a previous one would make this pass
      // for the wrong reason.
      await device.launchApp({
        delete: true,
        newInstance: true,
        url: connectLaunchUrl(stack.serverOriginForClient, stack.workspace.id),
        // The chat mounts during this launch and never goes idle. Android already disables Detox
        // sync from launchArgs; iOS must too, or launchApp starves getAgent (HTTP 499) and the
        // picker renders the prompt without any agent buttons.
        launchArgs: {
          detoxEnableSynchronization: 0,
          detoxURLBlacklistRegex: '\\(".*agent/events.*"\\)',
        },
      });
      await device.setURLBlacklist(['.*agent/events.*']);
      await device.disableSynchronization();
      await ensureConnected(stack.serverOriginForClient, stack.workspace.id, scenario.kind);
    });

    it('signs the agent in and leaves the session ready', async () => {
      const flow = harness.SIGN_IN_FLOWS[scenario.flow];
      await flow.beforeStart?.({ control: stack.control });

      // The harness mounts the chat directly, so the agent picker is the first thing here.
      await tap(`agent-picker-${scenario.agentId}`, 60_000);

      const offered = await harness.waitForAgentState(stack.serverUrl, stack.workspace.id, 'authentication_required');
      const methodId = offered.authMethods?.[0]?.id;
      if (!methodId) throw new Error('The agent offered no subscription sign-in method.');
      await tap(`agent-auth-method-${methodId}`);

      // The client can only act once the Server has said what kind of sign-in this is.
      const session = await harness.waitForChallenge(
        stack.serverUrl,
        stack.workspace.id,
        challenge => harness.isUsableChallenge(challenge, scenario),
      );

      await tap('agent-signin-open');

      const carriedCode = await flow.approve?.({ control: stack.control, session });
      if (carriedCode) {
        // Opening the sign-in page handed the foreground to the browser, which is what this shape
        // is: the page issues a code and the person carries it back. What a person does next is
        // switch back to the app, and until something does, there is no app to type into - iOS
        // suspends what is not in front, so it stops answering Detox at all. That reads as a tap
        // that was never delivered rather than as anything the client did, and it is exactly how
        // this scenario failed while the other three passed: they never return to the app.
        await device.launchApp({ newInstance: false, launchArgs: { detoxEnableSynchronization: 0 } });
        await device.disableSynchronization();

        // The pasted-code shape: the value travels back through the client, exactly as a user
        // carries it out of the browser.
        await waitFor(element(by.id('agent-signin-code-input'))).toBeVisible().withTimeout(30_000);
        await element(by.id('agent-signin-code-input')).typeText(carriedCode);
        await tap('agent-signin-submit-code');
      }

      await harness.waitForAgentState(stack.serverUrl, stack.workspace.id, 'ready', { timeoutMs: 150_000 });
    });
  });
});

async function ensureConnected(serverUrl, workspaceId, kind) {
  // The prompt renders before getAgent returns; the picker buttons do not. Wait for the button
  // this case will tap, so a cancelled first fetch cannot look like a connected chat.
  const picker = `agent-picker-${kind}`;
  try {
    await waitFor(element(by.id(picker))).toBeVisible().withTimeout(15_000);
    return;
  } catch {
    // Deep link missed; the connect form is the fallback.
  }
  if (await isVisible('server-url-input')) {
    await connectTo(serverUrl, workspaceId);
  }
  await waitFor(element(by.id(picker))).toBeVisible().withTimeout(60_000);
}

async function connectTo(serverUrl, workspaceId) {
  await waitFor(element(by.id('server-url-input'))).toBeVisible().withTimeout(60_000);
  await fill('server-url-input', serverUrl);
  await fill('workspace-id-input', workspaceId);
  await tap('server-connect');
  await waitFor(element(by.id('server-url-input'))).not.toBeVisible().withTimeout(15_000);
}

/** Android Fabric's replaceText does not fire onChangeText, so Connect would no-op. */
async function fill(testId, value) {
  const field = element(by.id(testId));
  await field.tap();
  await field.clearText();
  await field.typeText(value);
}

async function isVisible(testId) {
  try {
    await waitFor(element(by.id(testId))).toBeVisible().withTimeout(500);
    return true;
  } catch {
    return false;
  }
}

/** Every interaction waits for visibility first; nothing taps on faith. */
async function tap(testId, timeoutMs = 30_000) {
  await waitFor(element(by.id(testId))).toBeVisible().withTimeout(timeoutMs);
  await element(by.id(testId)).tap();
}
