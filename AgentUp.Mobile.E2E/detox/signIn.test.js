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
      // Again per case: a hosted emulator that drops its adb transport takes every reverse with
      // it, and the next case would then launch an app that cannot reach the Server. adb reverse
      // replaces an existing mapping, so this is a no-op when nothing was lost.
      await reverseHostPorts(stack.hostPorts);
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
      // Synchronization is off, so the picker tap returns before the session renders. The Server
      // can also report authentication_required before React Native has laid the method buttons
      // out. iOS Detox then logs "Layers needs layout" / "Main Queue pending" and a 30s
      // visibility wait expires while the picker and Open still use 60s. Keep sync off:
      // scheduleAgent has opened the event stream, and Detox waiting for idle is how the
      // picker itself used to starve.
      await device.disableSynchronization();
      await tap(`agent-auth-method-${methodId}`, 60_000);

      // The client can only act once the Server has said what kind of sign-in this is.
      const session = await harness.waitForChallenge(
        stack.serverUrl,
        stack.workspace.id,
        challenge => harness.isUsableChallenge(challenge, scenario),
        { timeoutMs: 120_000 },
      );

      // Paste is the only shape that must visit the provider before Open: the code has to exist
      // before the system browser takes the foreground. Device and poll complete sign-in on
      // approve, which unmounts Open, so those shapes tap Open first.
      const pasteFirst = scenario.flow === 'paste';
      const carriedCode = pasteFirst
        ? await flow.approve?.({ control: stack.control, session })
        : undefined;
      await tap('agent-signin-open', 60_000);
      if (!pasteFirst)
        await flow.approve?.({ control: stack.control, session });
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
        // carries it out of the browser. fill() taps first so Fabric onChangeText enables Finish;
        // typeText alone leaves the button disabled. iOS then covers Finish with the keyboard.
        // Detox 20.42's Jest device has no dismissKeyboard; Return blurs the field, then wait
        // until Finish is hittable instead of sleeping. Re-read latestCode after the browser
        // visit so a second authorize from Open is the value that is pasted.
        // Android TextInput is EditText: Detox toHaveText matches TextView and never sees the
        // value. Finish signing in is enabled only when React state holds the paste.
        await waitFor(element(by.id('agent-signin-code-input'))).toBeVisible().withTimeout(30_000);
        const code = await pastedCode(flow, stack.control, carriedCode);
        await fill('agent-signin-code-input', code);
        if (device.getPlatform() === 'ios') {
          await element(by.id('agent-signin-code-input')).tapReturnKey();
        }
        await waitFor(element(by.id('agent-signin-submit-code').and(by.enabled())))
          .toBeVisible()
          .withTimeout(15_000);
        await tap('agent-signin-submit-code', 60_000);
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
  const step = await waitForConnectStep(picker, 20_000, ready.connectStep, ready.connectStepPending);
  const recovery = ready.connectRecovery(step);
  if (recovery === 'none') return;
  if (recovery === 'form') {
    await connectTo(serverUrl, workspaceId);
  } else {
    // Prompt with no buttons: first getAgent died (event stream 404). Blank frame: dropped
    // launch URL. Re-deliver the URL so Chat remounts, then fill the form if that is what
    // came to the front.
    await recoverLaunch(serverUrl, workspaceId, ready.adbDismissAnrArgs);
    if (await appears('server-url-input', 15_000)) {
      await connectTo(serverUrl, workspaceId);
    }
  }
  // The picker renders once getAgent answers. On a three-core macOS runner that has just
  // reinstalled the app, the Server's first answer has arrived more than a minute after launch,
  // and a recovery remount cancels whatever request was already in flight, so the wait after a
  // recovery has to cover a cold answer plus a render.
  await waitForPicker(picker, PICKER_AFTER_CONNECT_MS);
}

const PICKER_AFTER_CONNECT_MS = 120_000;

async function waitForConnectStep(picker, timeoutMs, decide, pending) {
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
    if (!pending(last)) return last;
  }
  return last;
}

// iOS first launchApp can ignore detoxEnableSynchronization: 0. A single 60s waitFor then
// waits for idle while the chat's event stream and timers keep the app busy, and the picker
// buttons never match. Short visibility polls with sync off are the same contract as the
// connect-step wait.
async function waitForPicker(picker, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    await device.disableSynchronization();
    if (await appears(picker, 400)) return;
  }
  await device.disableSynchronization();
  await waitFor(element(by.id(picker))).toBeVisible().withTimeout(1_000);
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
  // One shot of 32-character PKCE on Android IME is how pasted-code submitted a truncated
  // value, the CLI exited 1, and the suite waited 150s for ready.
  const chunk = 8;
  for (let index = 0; index < value.length; index += chunk) {
    await field.typeText(value.slice(index, index + chunk));
  }
}

async function pastedCode(flow, control, fallback) {
  if (!flow.clientId) return fallback;
  try {
    return (await control.latestCode(flow.clientId)) || fallback;
  } catch {
    return fallback;
  }
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
