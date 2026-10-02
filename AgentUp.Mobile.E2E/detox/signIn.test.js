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
const { execFile } = require('node:child_process');
const { promisify } = require('node:util');
const { existsSync } = require('node:fs');
const { join } = require('node:path');

const execFileAsync = promisify(execFile);
const SYNC_OFF = {
  detoxEnableSynchronization: 0,
  detoxURLBlacklistRegex: '\\(".*agent/events.*"\\)',
};

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
    const [stack, flows, ready, anr] = await Promise.all([
      import('../harness/stack.mjs'),
      import('../harness/signInFlows.mjs'),
      import('../harness/connectReady.mjs'),
      import('../harness/androidAnr.mjs'),
    ]);
    harness = { ...stack, ...flows, ...ready, ...anr };
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
      // Reverse before launchApp: the chat fetches the Server the instant the connect URL lands.
      // QEMU 10.0.2.2 NAT ConnectException'd; cleartext to 127.0.0.1 is forbidden on the emulator.
      await reverseHostPorts(stack.hostPorts);
    });

    afterAll(async () => {
      await unreverseHostPorts(stack?.hostPorts);
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
        launchArgs: SYNC_OFF,
      });
      await device.setURLBlacklist(['.*agent/events.*']);
      await device.disableSynchronization();
      await ensureConnected(stack.serverOriginForClient, stack.workspace.id, scenario.agentId, harness);
    });

    it('signs the agent in and leaves the session ready', async () => {
      const flow = harness.SIGN_IN_FLOWS[scenario.flow];
      await flow.beforeStart?.({ control: stack.control });

      // The harness mounts the chat directly, so the agent picker is the first thing here.
      await tap(pickerFor(scenario.agentId, scenario.name), 60_000);

      const offered = await harness.waitForAgentState(stack.serverUrl, stack.workspace.id, 'authentication_required');
      const methodId = offered.authMethods?.[0]?.id;
      if (!methodId) throw new Error('The agent offered no subscription sign-in method.');
      await tap(`agent-auth-method-${methodId}`);

      // The client can only act once the Server has said what kind of sign-in this is.
      const session = await harness.waitForChallenge(
        stack.serverUrl,
        stack.workspace.id,
        challenge => harness.isUsableChallenge(challenge, scenario),
        { timeoutMs: 120_000 },
      );

      await tap('agent-signin-open', 60_000);

      const carriedCode = await flow.approve?.({ control: stack.control, session });
      if (carriedCode) {
        // Opening the sign-in page handed the foreground to the browser, which is what this shape
        // is: the page issues a code and the person carries it back. What a person does next is
        // switch back to the app, and until something does, there is no app to type into - iOS
        // suspends what is not in front, so it stops answering Detox at all. That reads as a tap
        // that was never delivered rather than as anything the client did, and it is exactly how
        // this scenario failed while the other three passed: they never return to the app.
        await device.launchApp({
          newInstance: false,
          url: connectLaunchUrl(stack.serverOriginForClient, stack.workspace.id),
          launchArgs: SYNC_OFF,
        });
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

// A scenario with no agentId would build `agent-picker-undefined`, which Detox and Playwright
// both report only as a matcher that never matched - sixty seconds later, naming neither the
// scenario nor the missing field. A rename that misses one table should say so at once.
function pickerFor(agentId, scenarioName) {
  if (!agentId) {
    throw new Error(
      `Scenario '${scenarioName}' has no agentId, so the agent picker id cannot be built. `
      + 'Agents are listed by capability module id (codex, cursor, claude).');
  }
  return `agent-picker-${agentId}`;
}

async function ensureConnected(serverUrl, workspaceId, agentId, ready) {
  // The prompt renders before getAgent returns; the picker buttons do not. Wait for the button
  // this case will tap, so a cancelled first fetch cannot look like a connected chat.
  // connectStep is passed in: these helpers sit outside describe, so they cannot close over
  // the describe-scoped harness (that is a ReferenceError, and every native scenario died on it).
  const picker = pickerFor(agentId, 'ensureConnected');
  const step = await waitForConnectStep(picker, 20_000, ready.connectStep);
  if (step === 'connected') return;
  if (step === 'fill-form') {
    await connectTo(serverUrl, workspaceId);
  } else if (step !== 'wait-agents') {
    // Deep link missed and neither the form nor the chat is in front: an ANR or a dropped
    // launch URL. Deliver the URL again after dismissing a focused ANR, then fill the form
    // if that is what came to the front.
    await recoverLaunch(serverUrl, workspaceId, ready.adbDismissAnrArgs);
    if (await appears('server-url-input', 15_000)) {
      await connectTo(serverUrl, workspaceId);
    }
  }
  await waitFor(element(by.id(picker))).toBeVisible().withTimeout(60_000);
}

async function waitForConnectStep(picker, timeoutMs, decide) {
  const deadline = Date.now() + timeoutMs;
  let last = 'relaunch';
  while (Date.now() < deadline) {
    const snapshot = {
      pickerVisible: await appears(picker, 400),
      formVisible: false,
      promptVisible: false,
    };
    if (!snapshot.pickerVisible) snapshot.formVisible = await appears('server-url-input', 400);
    if (!snapshot.pickerVisible && !snapshot.formVisible) {
      snapshot.promptVisible = await appears('agent-picker-prompt', 400);
    }
    last = decide(snapshot);
    if (last !== 'wait-agents') return last;
  }
  return last;
}

async function reverseHostPorts(ports) {
  if (device.getPlatform() !== 'android') return;
  for (const port of ports) {
    await device.reverseTcpPort(port);
  }
}

async function unreverseHostPorts(ports) {
  if (device.getPlatform() !== 'android' || !ports) return;
  for (const port of ports) {
    try {
      await device.unreverseTcpPort(port);
    } catch {
      // The mapping is gone when the emulator is already tearing down.
    }
  }
}

async function recoverLaunch(serverUrl, workspaceId, adbDismissAnrArgs) {
  if (device.getPlatform() === 'android') {
    await dismissAndroidAnrIfPresent(adbDismissAnrArgs);
  }
  await device.launchApp({
    newInstance: false,
    url: connectLaunchUrl(serverUrl, workspaceId),
    launchArgs: SYNC_OFF,
  });
  await device.setURLBlacklist(['.*agent/events.*']);
  await device.disableSynchronization();
}

async function dismissAndroidAnrIfPresent(adbDismissAnrArgs) {
  const adb = androidAdb();
  if (!adb) return;
  let stdout = '';
  try {
    ({ stdout } = await execFileAsync(adb, ['shell', 'dumpsys', 'window'], { timeout: 10_000 }));
  } catch {
    return;
  }
  const line = stdout.split('\n').find(row => row.includes('mCurrentFocus')) ?? '';
  for (const args of adbDismissAnrArgs(line)) {
    try {
      await execFileAsync(adb, args, { timeout: 5_000 });
    } catch {
      // Best-effort, same as the wake script: a device that already moved on is fine.
    }
  }
}

function androidAdb() {
  const home = process.env.ANDROID_HOME || process.env.ANDROID_SDK_ROOT;
  if (home) {
    const bundled = join(home, 'platform-tools', 'adb');
    if (existsSync(bundled)) return bundled;
  }
  return 'adb';
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

async function appears(testId, timeoutMs = 500) {
  try {
    await waitFor(element(by.id(testId))).toBeVisible().withTimeout(timeoutMs);
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
