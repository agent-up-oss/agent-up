import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const { connectLaunchUrl, parseConnectLaunchUrl } = require('../../AgentUp.Mobile.E2E.App/src/connectLaunch');

test('native Detox can hand the Server origin and workspace to the harness as a launch URL', () => {
  const href = connectLaunchUrl('http://10.0.2.2:24001/', 'workspace-1');
  assert.deepEqual(parseConnectLaunchUrl(href), {
    url: 'http://10.0.2.2:24001',
    workspaceId: 'workspace-1',
  });
});

test('a connect launch URL that is not this app is ignored', () => {
  assert.equal(parseConnectLaunchUrl(null), null);
  assert.equal(parseConnectLaunchUrl('not a url'), null);
  assert.equal(parseConnectLaunchUrl('http://localhost:5000'), null);
  assert.equal(parseConnectLaunchUrl('agent-up-chat://connect'), null);
  assert.equal(parseConnectLaunchUrl('agent-up-chat://connect?server=http://10.0.2.2:1'), null);
});
