import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const { allowAndroidCleartext, allowIosLocalHttp } = require('../../AgentUp.Mobile.E2E.App/plugins/withLocalHttpTransport');

test('the Android harness can reach ephemeral HTTP services on the CI host', () => {
  const manifest = { manifest: { application: [{ $: {} }] } };
  allowAndroidCleartext(manifest);
  assert.equal(manifest.manifest.application[0].$['android:usesCleartextTraffic'], 'true');
});

test('the iOS harness can reach ephemeral HTTP services on the CI host', () => {
  const infoPlist = { NSAppTransportSecurity: { ExistingSetting: true } };
  allowIosLocalHttp(infoPlist);
  assert.deepEqual(infoPlist.NSAppTransportSecurity, {
    ExistingSetting: true,
    NSAllowsArbitraryLoads: true,
    NSAllowsLocalNetworking: true,
  });
});
