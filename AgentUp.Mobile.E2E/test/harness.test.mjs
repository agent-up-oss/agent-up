import assert from 'node:assert/strict';
import test from 'node:test';

import { createIdpControl, loginIdFrom } from '../harness/idpControl.mjs';
import { hasTransport, isTerminalSignInError, isUsableChallenge, waitForChallenge } from '../harness/signInFlows.mjs';
import { shimScript } from '../harness/shims.mjs';
import { AGENT_PROFILES, hostOriginFor, hostPortsToReverse, profilesFor, serverEnvironment } from '../harness/stackConfig.mjs';
import { freePort, portWindow, removeWorkspaceTree } from '../harness/stack.mjs';
import { waitFor } from '../harness/wait.mjs';

test('each stack serves three agent modules, with the codex slot chosen explicitly', () => {
  const device = profilesFor('device');
  const redirect = profilesFor('redirect');

  assert.deepEqual(device.map(p => p.agentId).sort(), ['claude', 'codex', 'cursor']);
  assert.equal(device.find(p => p.agentId === 'codex').agent, 'test-agent2');
  assert.equal(redirect.find(p => p.agentId === 'codex').agent, 'test-agent1');
  assert.throws(() => profilesFor('poll'), /takes 'device' or 'redirect'/);
});

test('the Server is told each agent command, login command, and the transport it implements', () => {
  const environment = serverEnvironment({
    profiles: profilesFor('device'),
    binDir: '/tmp/bin',
    idpUrl: 'http://localhost:9000',
    publicOrigin: 'http://localhost:9000',
    dataDir: '/tmp/data',
    urls: 'http://0.0.0.0:9100',
  });

  assert.equal(environment.Agents__codex__Command, '/tmp/bin/test-agent2');
  assert.equal(environment.Agents__codex__Arguments__0, 'acp');
  assert.equal(environment.Agents__codex__LoginCommand, '/tmp/bin/test-agent2');
  assert.equal(environment.Agents__codex__LoginArguments__0, 'login');
  assert.equal(environment.Agents__codex__LoginTransport, 'device');
  assert.equal(environment.Agents__claude__LoginTransport, 'paste');
  assert.equal(environment.Agents__cursor__LoginTransport, 'poll');
  // The login process needs to know which provider to sign in against and which agent it is.
  assert.equal(environment.Agents__claude__LoginEnvironment__AGENTUP_TEST_IDP_URL, 'http://localhost:9000');
  assert.equal(environment.Agents__claude__LoginEnvironment__AGENTUP_TEST_AGENT, 'test-agent3');
  // And where the person reaches it, which on a device is somewhere else entirely. Without this
  // an agent prints a link on its own origin and the device cannot open it.
  assert.equal(
    environment.Agents__claude__LoginEnvironment__AGENTUP_TEST_IDP_PUBLIC_ORIGIN,
    'http://localhost:9000',
  );
  // Agent sign-in is the subject; Server sign-in is not.
  assert.equal(environment.AGENTUP_AUTH_DISABLED, 'true');
});

test('each stack writes Server state into its own data directory', () => {
  const environment = serverEnvironment({
    profiles: profilesFor('device'),
    binDir: '/tmp/bin',
    idpUrl: 'http://localhost:9000',
    publicOrigin: 'http://localhost:9000',
    dataDir: '/tmp/data',
    urls: 'http://0.0.0.0:9100',
  });

  assert.equal(environment.Storage__DataDirectory, '/tmp/data');
});

// The mirror of what the agents do: a link is minted for the person, and neither the agent nor
// this harness is the person. Both keep the path and drop the host.
test('the control plane can reach a page the device was pointed at', () => {
  const control = createIdpControl('http://localhost:9000');

  assert.equal(
    control.reachable('http://127.0.0.1:9000/oauth/authorize?client_id=test-agent3&state=abc'),
    'http://localhost:9000/oauth/authorize?client_id=test-agent3&state=abc',
  );
  // A leftover 10.0.2.2 challenge is rewritten the same way: that address is the emulator's
  // name for this host, and nothing here answers to it.
  assert.equal(
    control.reachable('http://10.0.2.2:9000/oauth/authorize?client_id=test-agent3&state=abc'),
    'http://localhost:9000/oauth/authorize?client_id=test-agent3&state=abc',
  );
  // Already reachable is left exactly as it was, so the simulator and the web build are untouched.
  assert.equal(
    control.reachable('http://localhost:9000/login/xyz'),
    'http://localhost:9000/login/xyz',
  );
});

test('the Server is given deadlines short enough to fail a hung sign-in legibly', () => {
  const environment = serverEnvironment({
    profiles: profilesFor('redirect'),
    binDir: '/tmp/bin',
    idpUrl: 'http://localhost:9000',
    publicOrigin: 'http://localhost:9000',
    dataDir: '/tmp/data',
    urls: 'http://0.0.0.0:9100',
    challengeTimeoutSeconds: 15,
    completionTimeoutSeconds: 45,
  });

  assert.equal(environment.Agents__codex__LoginChallengeTimeoutSeconds, '15');
  assert.equal(environment.Agents__codex__LoginCompletionTimeoutSeconds, '45');
});

// An Android emulator is a separate network namespace. Getting this wrong is not a flake, it is a
// total failure to connect, so it is pinned rather than left to a per-test guess. Hosted CI cannot
// rely on QEMU's 10.0.2.2 NAT; Detox reverseTcpPort exposes the host on emulator loopback.
test('each client platform is pointed at the host origin it can actually reach', () => {
  assert.equal(hostOriginFor('android', 9000), 'http://localhost:9000');
  assert.equal(hostOriginFor('ios', 9000), 'http://localhost:9000');
  assert.equal(hostOriginFor('web', 9000), 'http://localhost:9000');
  assert.throws(() => hostOriginFor('windows-phone', 9000), /Unknown client platform/);
});

test('Android clients use the localhost hostname, not the 127.0.0.1 IP literal', () => {
  assert.equal(new URL(hostOriginFor('android', 24001)).hostname, 'localhost');
});

test('Android reverse maps the Server and identity-provider ports onto the emulator', () => {
  assert.deepEqual(hostPortsToReverse(24001, 24000), [24001, 24000]);
  assert.throws(() => hostPortsToReverse(0, 24000), /cannot be reversed/);
  assert.throws(() => hostPortsToReverse(24001, 1.5), /cannot be reversed/);
});

test('native Detox reverses those host ports before launchApp', async () => {
  const { readFile } = await import('node:fs/promises');
  const source = await readFile(new URL('../detox/signIn.test.js', import.meta.url), 'utf8');
  const beforeAll = source.slice(source.indexOf('stack = await harness.startStack'), source.indexOf('beforeEach'));
  assert.match(
    beforeAll,
    /await reverseHostPorts\(stack\.hostPorts\)/,
    'The chat fetches the Server as soon as the connect URL lands, so reverse must happen in the same beforeAll that starts the stack.',
  );
  assert.equal(beforeAll.includes('device.launchApp'), false);
});

test('each agent gets a launcher that names it explicitly', () => {
  const script = shimScript('/opt/agents/agent-up-test-agent', 'test-agent3');

  assert.match(script, /^#!\/bin\/sh$/m);
  assert.match(script, /AGENTUP_TEST_AGENT='test-agent3'/);
  assert.match(script, /exec '\/opt\/agents\/agent-up-test-agent' "\$@"/);
});

test('every sign-in shape is covered by a distinct agent', () => {
  const agents = Object.values(AGENT_PROFILES).map(profile => profile.agent);
  assert.equal(new Set(agents).size, 4, 'four shapes, four agents');
});

test('a login id is read back out of a deep link', () => {
  assert.equal(loginIdFrom('http://10.0.2.2:9000/login/abc123'), 'abc123');
  assert.throws(() => loginIdFrom('http://10.0.2.2:9000/device'), /is not a sign-in deep link/);
  assert.throws(() => loginIdFrom(undefined), /is not a sign-in deep link/);
});

test('the control plane posts approvals as form values the provider understands', async () => {
  const calls = [];
  const request = async (url, options) => {
    calls.push({ url, body: options?.body });
    return { ok: true, json: async () => ({ approved: true }) };
  };

  const control = createIdpControl('http://localhost:9000/', request);
  await control.approveUserCode('ABCD-EFGH');
  await control.approveLogin('abc123');
  await control.preApprove('test-agent1');

  assert.deepEqual(calls.map(call => call.url), [
    'http://localhost:9000/test/approve',
    'http://localhost:9000/test/approve',
    'http://localhost:9000/test/pre-approve',
  ]);
  assert.equal(calls[0].body, 'user_code=ABCD-EFGH');
  assert.equal(calls[1].body, 'login_id=abc123');
  assert.equal(calls[2].body, 'client_id=test-agent1');
});

test('a wait that never succeeds names what it was waiting for', async () => {
  await assert.rejects(
    () => waitFor('something that never happens', () => false, { timeoutMs: 60, intervalMs: 10 }),
    /Timed out after 60ms waiting for something that never happens/,
  );
});

test('a wait reports the last error it saw rather than swallowing it', async () => {
  await assert.rejects(
    () => waitFor('a reachable service', () => { throw new Error('connection refused'); }, { timeoutMs: 60, intervalMs: 10 }),
    /Last error: connection refused/,
  );
});

test('ACP auth-required on the session is not a failed sign-in', () => {
  assert.equal(
    isTerminalSignInError('{"code":-32000,"message":"Authentication required. Sign in with your subscription to continue."}'),
    false,
  );
  assert.equal(isTerminalSignInError('The agent CLI did not print a sign-in link within 30 seconds.'), true);
  assert.equal(isTerminalSignInError(null), false);
});

test('a failed sign-in is reported instead of waiting out the challenge deadline', async () => {
  const original = globalThis.fetch;
  globalThis.fetch = async () => ({
    ok: true,
    json: async () => ({
      state: 'authenticating',
      error: 'The agent CLI did not print a sign-in link within 30 seconds.',
      loginChallenge: null,
    }),
  });
  try {
    await assert.rejects(
      () => waitForChallenge('http://localhost:9', 'workspace', () => true, { timeoutMs: 5_000, intervalMs: 10 }),
      /did not print a sign-in link/,
    );
  } finally {
    globalThis.fetch = original;
  }
});

// The Server serialises the transport as its enum member name, so the wire carries 'Code' where
// both the client's contract and these scenarios say 'code'. Comparing the two raw is how every
// scenario timed out waiting for a challenge that had already arrived.
test('a challenge matches its transport whichever way the Server spelled it', () => {
  assert.equal(hasTransport({ transport: 'Code' }, 'code'), true);
  assert.equal(hasTransport({ transport: 'code' }, 'code'), true);
  assert.equal(hasTransport({ transport: 'Redirect' }, 'redirect'), true);
  assert.equal(hasTransport({ transport: 'Poll' }, 'code'), false);
  assert.equal(hasTransport({ transport: null }, 'code'), false);
  assert.equal(hasTransport(null, 'code'), false);
});

// Device-code CLIs print the URL first. Treating that as ready is how the installable-web
// device-code scenario failed while pasted-code, poll, and redirect on the same run passed.
test('a device-code challenge is not usable until the user code arrives', () => {
  const device = { flow: 'device', transport: 'code' };
  assert.equal(
    isUsableChallenge({ url: 'https://auth.openai.com/codex/device', transport: 'Code' }, device),
    false,
  );
  assert.equal(
    isUsableChallenge(
      { url: 'https://auth.openai.com/codex/device', transport: 'Code', code: 'ABCD-EFGH' },
      device,
    ),
    true,
  );
});

test('a pasted-code challenge is usable from the URL, because the code comes from the provider page', () => {
  assert.equal(
    isUsableChallenge(
      { url: 'https://claude.ai/oauth/authorize', transport: 'Code' },
      { flow: 'paste', transport: 'code' },
    ),
    true,
  );
});

// Scenarios run side by side, so two stacks asking for a port at the same moment must not be
// able to receive the same one. Each worker walks its own window and never repeats.
test('teardown deletes a nested chromium download tree', async () => {
  const { mkdtemp, mkdir, writeFile, access } = await import('node:fs/promises');
  const { constants } = await import('node:fs');
  const { tmpdir } = await import('node:os');
  const { join } = await import('node:path');
  const root = await mkdtemp(join(tmpdir(), 'agent-up-e2e-rm-'));
  const locales = join(root, 'data', 'chromium', 'Chrome', 'Linux-154.0.8037.57', 'chrome-linux64', 'locales');
  await mkdir(locales, { recursive: true });
  await writeFile(join(locales, 'en-US.pak'), 'x');
  await writeFile(join(locales, '..', 'chrome'), 'x');
  await removeWorkspaceTree(root);
  await assert.rejects(() => access(root, constants.F_OK), { code: 'ENOENT' });
});

test('a worker hands out distinct ports from a window it owns alone', async () => {
  const ports = [];
  for (let index = 0; index < 5; index++) ports.push(await freePort());

  assert.equal(new Set(ports).size, ports.length);
  for (const port of ports) {
    assert.ok(port >= portWindow.base && port < portWindow.base + portWindow.size, `${port} is outside the window`);
  }
});
