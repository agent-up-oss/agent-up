/**
 * Agent sign-in, end to end, on a real simulator and a real emulator.
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

// Named here rather than imported, so the matrix is readable without opening the harness.
const SCENARIOS = [
  { name: 'device code', flow: 'device', codexSchema: 'device', kind: 'Codex', transport: 'code' },
  { name: 'pasted code', flow: 'paste', codexSchema: 'device', kind: 'Claude', transport: 'code' },
  { name: 'poll until approved', flow: 'poll', codexSchema: 'device', kind: 'Cursor', transport: 'poll' },
  { name: 'loopback redirect', flow: 'redirect', codexSchema: 'redirect', kind: 'Codex', transport: 'redirect' },
];

describe('agent sign-in', () => {
  let harness;

  beforeAll(async () => {
    // The harness is ESM and this runner is CommonJS, so it is brought in dynamically.
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
      await device.launchApp({ delete: true, newInstance: true });
      await connectTo(stack.serverOriginForClient);
    });

    it('signs the agent in and leaves the session ready', async () => {
      const flow = harness.SIGN_IN_FLOWS[scenario.flow];
      await flow.beforeStart?.({ control: stack.control });

      // Workspaces are chosen from the shell's sidebar, which starts closed, and land on the
      // workspace dashboard. The agent chat is one step further in.
      await tap('open-sidebar', 60_000);
      await tap(`workspace-${stack.workspace.id}`, 60_000);
      await tap('open-workspace-agent', 60_000);
      await tap(`agent-picker-${scenario.kind}`, 60_000);

      const offered = await harness.waitForAgentState(stack.serverUrl, stack.workspace.id, 'authentication_required');
      const methodId = offered.authMethods?.[0]?.id;
      if (!methodId) throw new Error('The agent offered no subscription sign-in method.');
      await tap(`agent-auth-method-${methodId}`);

      // The client can only act once the Server has said what kind of sign-in this is.
      const session = await harness.waitForChallenge(
        stack.serverUrl,
        stack.workspace.id,
        challenge => challenge.url !== null && challenge.transport === scenario.transport,
      );

      await tap('agent-signin-open');

      const carriedCode = await flow.approve?.({ control: stack.control, session });
      if (carriedCode) {
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

async function connectTo(serverUrl) {
  await waitFor(element(by.id('server-url-input'))).toBeVisible().withTimeout(60_000);
  await element(by.id('server-url-input')).replaceText(serverUrl);
  await tap('server-connect');
}

/** Every interaction waits for visibility first; nothing taps on faith. */
async function tap(testId, timeoutMs = 30_000) {
  await waitFor(element(by.id(testId))).toBeVisible().withTimeout(timeoutMs);
  await element(by.id(testId)).tap();
}
