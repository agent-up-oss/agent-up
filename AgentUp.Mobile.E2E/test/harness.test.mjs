import assert from 'node:assert/strict';
import test from 'node:test';

import { createIdpControl, loginIdFrom } from '../harness/idpControl.mjs';
import { hasTransport } from '../harness/signInFlows.mjs';
import { shimScript } from '../harness/shims.mjs';
import { AGENT_PROFILES, hostOriginFor, profilesFor, serverEnvironment } from '../harness/stackConfig.mjs';
import { freePort, portWindow } from '../harness/stack.mjs';
import { waitFor } from '../harness/wait.mjs';

test('each stack serves three agent kinds, with the Codex slot chosen explicitly', () => {
  const device = profilesFor('device');
  const redirect = profilesFor('redirect');

  assert.deepEqual(device.map(p => p.kind).sort(), ['Claude', 'Codex', 'Cursor']);
  assert.equal(device.find(p => p.kind === 'Codex').agent, 'test-agent2');
  assert.equal(redirect.find(p => p.kind === 'Codex').agent, 'test-agent1');
  assert.throws(() => profilesFor('poll'), /takes 'device' or 'redirect'/);
});

test('the Server is told each agent command, login command, and the transport it implements', () => {
  const environment = serverEnvironment({
    profiles: profilesFor('device'),
    binDir: '/tmp/bin',
    idpUrl: 'http://localhost:9000',
    publicOrigin: 'http://10.0.2.2:9000',
    dataDir: '/tmp/data',
    urls: 'http://0.0.0.0:9100',
  });

  assert.equal(environment.Agents__Codex__Command, '/tmp/bin/test-agent2');
  assert.equal(environment.Agents__Codex__Arguments__0, 'acp');
  assert.equal(environment.Agents__Codex__LoginCommand, '/tmp/bin/test-agent2');
  assert.equal(environment.Agents__Codex__LoginArguments__0, 'login');
  assert.equal(environment.Agents__Codex__LoginTransport, 'device');
  assert.equal(environment.Agents__Claude__LoginTransport, 'paste');
  assert.equal(environment.Agents__Cursor__LoginTransport, 'poll');
  // The login process needs to know which provider to sign in against and which agent it is.
  assert.equal(environment.Agents__Claude__LoginEnvironment__AGENTUP_TEST_IDP_URL, 'http://localhost:9000');
  assert.equal(environment.Agents__Claude__LoginEnvironment__AGENTUP_TEST_AGENT, 'test-agent3');
  // And where the person reaches it, which on a device is somewhere else entirely. Without this
  // an agent prints a link on its own origin and the device cannot open it.
  assert.equal(
    environment.Agents__Claude__LoginEnvironment__AGENTUP_TEST_IDP_PUBLIC_ORIGIN,
    'http://10.0.2.2:9000',
  );
  // Agent sign-in is the subject; Server sign-in is not.
  assert.equal(environment.AGENTUP_AUTH_DISABLED, 'true');
});

// The mirror of what the agents do: a link is minted for the person, and neither the agent nor
// this harness is the person. Both keep the path and drop the host.
test('the control plane can reach a page the device was pointed at', () => {
  const control = createIdpControl('http://localhost:9000');

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

  assert.equal(environment.Agents__Codex__LoginChallengeTimeoutSeconds, '15');
  assert.equal(environment.Agents__Codex__LoginCompletionTimeoutSeconds, '45');
});

// An Android emulator is a separate network namespace. Getting this wrong is not a flake, it is a
// total failure to connect, so it is pinned rather than left to a per-test guess.
test('each client platform is pointed at the host origin it can actually reach', () => {
  assert.equal(hostOriginFor('android', 9000), 'http://10.0.2.2:9000');
  assert.equal(hostOriginFor('ios', 9000), 'http://localhost:9000');
  assert.equal(hostOriginFor('web', 9000), 'http://localhost:9000');
  assert.throws(() => hostOriginFor('windows-phone', 9000), /Unknown client platform/);
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

// Scenarios run side by side, so two stacks asking for a port at the same moment must not be
// able to receive the same one. Each worker walks its own window and never repeats.
test('a worker hands out distinct ports from a window it owns alone', async () => {
  const ports = [];
  for (let index = 0; index < 5; index++) ports.push(await freePort());

  assert.equal(new Set(ports).size, ports.length);
  for (const port of ports) {
    assert.ok(port >= portWindow.base && port < portWindow.base + portWindow.size, `${port} is outside the window`);
  }
});
